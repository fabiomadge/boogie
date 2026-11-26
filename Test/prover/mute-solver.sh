#!/bin/bash
# Closes its output and keeps reading, so that Boogie reaches the end of the output while its writes still succeed.
exec 1>&-
cat > /dev/null
