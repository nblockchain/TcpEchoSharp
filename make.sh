#!/usr/bin/env bash
set -e

source ./build.config
$FsxRunner ./make.fsx "$@"
