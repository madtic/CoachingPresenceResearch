using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Collections;
using System.Threading;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Security;
using RunProcessAsTask;

namespace CPR_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public override void EndInit()
        {
            base.EndInit();

            OutoutLabel.Content = "To slect a video for transcoding and encryption click the button below.";
        }

        internal static string HandBreakeCLI => AppDomain.CurrentDomain.BaseDirectory + "HandBrakeCLI.exe";
        internal static string PublicKey => AppDomain.CurrentDomain.BaseDirectory + "publickey.asc";

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog {Filter = "All Files|*.*"};  //get file path
            if (ofd.ShowDialog().GetValueOrDefault())
            {
                if (!File.Exists(ofd.FileName))
                {
                    MessageBox.Show("Could not open video file.","No File?", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                try
                {
                    OutoutLabel.Content = "Transcoding the video file, please wait.";
                    selectfilebutton.IsEnabled = false;
                    spinner.Visibility = Visibility.Visible;
                    errorIcon.Visibility = Visibility.Hidden;
                    succesIcon.Visibility = Visibility.Hidden;



                    string transcode = await Task.Run(() => transcodeVideoAsync(ofd.FileName));
                    if (File.Exists(transcode))
                    {
                        OutoutLabel.Content = "Transcoding complete, started encryption.";

                        string outputFile = ofd.FileName + ".pgp";
                        await Task.Run(() => EncryptPgpFile(transcode, outputFile));

                        OutoutLabel.Content = "Cleaning in progress.";
                        await Task.Run(() => File.Delete(transcode));   //remove temp file
                        spinner.Visibility = Visibility.Hidden;
                        succesIcon.Visibility = Visibility.Visible;

                        selectfilebutton.IsEnabled = true;
                        OutoutLabel.Content = "Complete.";

                        if(MessageBox.Show("Completed transcoding and encrypting video file. Show folder containing newly created file?","Success", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            Process.Start("explorer.exe", System.IO.Path.GetDirectoryName(outputFile));
                        }
                    }
                    else
                    {
                        spinner.Visibility = Visibility.Hidden;
                        errorIcon.Visibility = Visibility.Visible;
                        selectfilebutton.IsEnabled = true;
                        OutoutLabel.Content = "Failed, to slect a video for transcoding and encryption click the button below.";

                        MessageBox.Show("Could not transcode video file.", "Video Transcode Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    spinner.Visibility = Visibility.Hidden;
                    errorIcon.Visibility = Visibility.Visible;
                    selectfilebutton.IsEnabled = true;
                    OutoutLabel.Content = "Failed, to slect a video for transcoding and encryption click the button below.";

                    string path = AppDomain.CurrentDomain.BaseDirectory + "log.txt";
                    string[] content = {ex.Message, ex.StackTrace};
                    File.WriteAllLines(path, content);
                    MessageBox.Show(ex.Message,"Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region transcode

        string transcodeVideoAsync(string fileName)
        {
            string transcodedFile = System.IO.Path.GetTempPath() + Guid.NewGuid() + ".temp";
            string command =
                $"{HandBreakeCLI} --preset \"Very Fast 720p30\" --audio none -i \"{fileName}\" -o \"{transcodedFile}\"";

            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                UseShellExecute = true,
                CreateNoWindow = true,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true,
                //WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            

            var result = ProcessEx.RunAsync(info).Result;

            //Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ////* Create your Process
            //Process process = new Process
            //{
            //    StartInfo =
            //    {
            //        FileName = "cmd.exe",
            //        Arguments = "/c " + command,
            //        UseShellExecute = false,
            //        CreateNoWindow = false,
            //        RedirectStandardOutput = true,
            //        RedirectStandardError = true,
            //        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            //    }
            //};
            ////* Set your output and error (asynchronous) handlers
            ////process.OutputDataReceived += Process_OutputDataReceived;
            ////process.ErrorDataReceived += Process_ErrorDataReceived;
            ////* Start process and handlers
            //process.Start();
            ////process.BeginOutputReadLine();
            ////process.BeginErrorReadLine();
            //process.WaitForExit();

            return transcodedFile;
        }

        //private  void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        //{
        //    //MessageBox.Show(e.Data,"ErrorDataReceived", MessageBoxButton.OK, MessageBoxImage.Warning);
        //}

        //private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        //{
        //    //this.OutoutLabel.Content = e.Data;
        //}

        #endregion

        #region encryption

        private static PgpPublicKey ReadPublicKey(Stream inputStream)
        {
            inputStream = PgpUtilities.GetDecoderStream(inputStream);
            PgpPublicKeyRingBundle pgpPub = new PgpPublicKeyRingBundle(inputStream);

            foreach (PgpPublicKeyRing keyRing in pgpPub.GetKeyRings())
            {
                foreach (PgpPublicKey key in keyRing.GetPublicKeys())
                {
                    if (key.IsEncryptionKey)
                    {
                        return key;
                    }
                }
            }

            throw new ArgumentException("Can't find encryption key in key ring.");
        }

        public static void EncryptPgpFile(string inputFile, string outputFile)
        {
            // use armor: yes, use integrity check? yes?
            EncryptPgpFile(inputFile, outputFile, PublicKey, true, true);
        }

        public static void EncryptPgpFile(string inputFile, string outputFile, string publicKeyFile, bool armor, bool withIntegrityCheck)
        {
            using (Stream publicKeyStream = File.OpenRead(publicKeyFile))
            {
                PgpPublicKey pubKey = ReadPublicKey(publicKeyStream);

                using (MemoryStream outputBytes = new MemoryStream())
                {
                    PgpCompressedDataGenerator dataCompressor = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
                    PgpUtilities.WriteFileToLiteralData(dataCompressor.Open(outputBytes), PgpLiteralData.Binary, new FileInfo(inputFile));

                    dataCompressor.Close();
                    PgpEncryptedDataGenerator dataGenerator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, withIntegrityCheck, new SecureRandom());

                    dataGenerator.AddMethod(pubKey);
                    byte[] dataBytes = outputBytes.ToArray();

                    using (Stream outputStream = File.Create(outputFile))
                    {
                        if (armor)
                        {
                            using (ArmoredOutputStream armoredStream = new ArmoredOutputStream(outputStream))
                            {
                                WriteStream(dataGenerator.Open(armoredStream, dataBytes.Length), ref dataBytes);
                            }
                        }
                        else
                        {
                            WriteStream(dataGenerator.Open(outputStream, dataBytes.Length), ref dataBytes);
                        }
                    }
                }
            }
         }

        public static void WriteStream(Stream inputStream, ref byte[] dataBytes)
        {
            using (Stream outputStream = inputStream)
            {
                outputStream.Write(dataBytes, 0, dataBytes.Length);
            }
        }

        #endregion

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);


        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            Button_Click(sender, e);
        }
    }
}
