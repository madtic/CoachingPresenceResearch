#!/bin/sh

echo -e "starting to install required software using Homebrew"

/usr/bin/ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)"

brew install handbrake

if brew ls --versions handbrake > /dev/null; then
  echo installed HandBreak
else
  echo failed to install handbreak, please contact support at support@coachingpresenceresearch.com
  exit 1
fi

brew install gnupg

if brew ls --versions gnupg > /dev/null; then
  echo installed GnuPG
else
  echo failed to install GnuPG, please contact support at support@coachingpresenceresearch.com
  exit 1
fi

echo start configure encryption from "$(dirname "$0")"
gpg --import "$(dirname "$0")"/publickey.asc
#gpg --edit-key tuende.erdoes@ptc-coaching.com
eval $(echo -e "5\ny\n" |  gpg --command-fd 0 --expert --edit-key tuende.erdoes@ptc-coaching.com trust)

echo
echo finished install script, this terminal window can now be closed 
