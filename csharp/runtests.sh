#!/bin/sh

# Runs all tests in the short form
# First argument is the name of bin directory where the executables are

cd ${1}
mono Tests.exe -fast

#mono ${1}/TestReplic.exe master&
#mono ${1}/TestReplic.exe slave
