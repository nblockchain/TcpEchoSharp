#!/bin/bash
#

PROJECT=`find . -maxdepth 1 -type f -iname \*.sln -exec basename {} \;`

echo "Restoring packages..."
msbuild -t:restore $PROJECT
echo "Building source..."
msbuild -t:build $PROJECT 

check()
{
    echo "Running net461 tests..."
    find . -regextype sed -regex  ".*/\net461\/ClientTests.dll$" -exec nunit-console {} \;
}

"$@"
