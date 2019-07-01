#!/bin/bash
#

PATH1=`pwd`
echo $PATH1;
find . -maxdepth 1 -type f -iname \*.sln > tmp1.out
COUNT=`cat tmp1.out | wc -l`

if [ $COUNT = 1 ]; then
while read i; do
PROJECT=$i;
echo $PROJECT;
done < tmp1.out
        else
            	if [ $COUNT = 0 ]; then
                                echo "No .sln found"
                        exit 1
                fi
        echo "ERROR: too many .sln files";
        exit 1
fi
echo "Restoring packages..."
msbuild -t:restore $PROJECT
echo "Building source..."
msbuild -t:build $PROJECT | tee tmp2.out

check()
{
echo "Running net461 tests..."
find . -regextype sed -regex  ".*/\net461\/ClientTests.dll$" -exec nunit-console {} \;
}

"$@"