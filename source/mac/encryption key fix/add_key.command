#!/bin/sh
echo start configure encryption from "$(dirname "$0")"
gpg --import "$(dirname "$0")"/publickey.asc
for fpr in $(gpg --list-keys --with-colons  | awk -F: '/fpr:/ {print $10}' | sort -u); do  echo -e "5\ny\n" |  gpg --command-fd 0 --expert --edit-key $fpr trust; done
