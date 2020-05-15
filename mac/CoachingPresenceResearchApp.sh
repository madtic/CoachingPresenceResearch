#!/bin/sh

originalFile="$1"
if [ ! -f "$originalFile" ]; then
	echo "ALERT:Invalid file selected!|Please select only video files."
    echo "Invalid file selected!"	
	sleep 5
	exit 1
fi

echo "starting compression process…"
sleep 3
/usr/local/bin/handbrakecli --preset "Very Fast 720p30" --audio none -i "$1" -o "$1.temp"

tempFile="$1.temp"
if [ ! -f "$tempFile" ]; then
    echo "Compression failed!"	
	echo "ALERT:Compression failed!|Failed to create compressed file. Please check if selected file was of type video file."
	sleep 5
	exit 1
fi
echo "Compression complete!"
sleep 2

echo "starting encryption process…"
sleep 3
/usr/local/bin/gpg --batch --yes --always-trust --output "$1.pgp" --encrypt --recipient tuende.erdoes@ptc-coaching.com "$1.temp"

encryptedFile="$1.pgp"
if [ ! -f "$encryptedFile" ]; then
    echo "Encryption failed!"	
	echo "ALERT:Encryption failed!|Failed to encrypt file $1.temp"
	sleep 5
	exit 1
fi

echo "encryption complete!"
sleep 2

rm "$1.temp"
echo "Process complete!"
echo "ALERT:Completed transcoding and encryption!|Ready to upload file: $1.pgp"
echo "NOTIFICATION:Completed transcoding and encryption!"

sleep 5


