# ExtractMDRFtoCSV ReadMe

#ExtractMDRFtoCSV #MDRF

## Overview

This document provides an introduction to the use of the ExtractMDRFtoCSV tool.  This document assumes 
that you already have a built copy of it.

The ExtractMDRFtoCSV (ExtractMDRFtoCSV.exe application) supports the ability to perform basic review 
of the contents of one or more MDRF files and supports the ability to extract selected contents from 
such files and to generate CSV style output files from them which can be used with other data analysis 
and charting software.

## System requirements

This tool has been tested on Windows 10, Windows 7, and Windows XP.  

This tool requires that the target computer already has the Microsoft DotNet 4 Client Profile product 
installed (or any superset of that such as the full version of Microsoft DotNet 4).

## Obtain and build source

If you do not already have this tool available and/or would like to build it from source, the source 
code can be obtained from github by cloning the https://github.com/mosaicsys/MosaicLibCS.git 
repository and the switching (checking out) a relatively recent rel_ or prerel_ branch (newer than 0.1.6.0).  
The ExtractMDRFtoCSV.csproj project (Base/Xtras/Tools/ExtractMDRFtoCSV/ExtractMDRFtoCSV.csproj) can be 
built under the Tools.sln solution (Base/Xtras/Tools/Tools.sln).

## ExtractMDRFtoCSV

The ExtractMDRFtoCSV.exe program is used to review the general contents of one or more MDRF file 
and to allow some or all of the contents of such MDRF files to be extracted to CSV style output files 
with, or without, interspersed occurrence information.

Generally the resulting CSV files can be used with external data analysis or charting products such 
as Excel.

### Normal use

This program is typically run from a Console command line or using a batch file.  The use of a batch 
file simplifies the ability to perform a set of pre-defined extraction operations and is typically 
used on all MDRF files in a given directory by using wildcard expansion (*.mdrf for example).

If the program is run without any arguments it will produce (nearly) the following usage (help) 
output:

```
Usage: 

ExtractMDRFtoCSV [-IncludeOccurrences | -IncludeExtras | -Sparse | -NoData | -HeaderAndDataOnly | -MapBool | -interval:interval | -start:deltaTime | -end:deltaTime | -tail:period | -group:name | -tag:tag] [fileName.mdrf] ...

ExtractMDRFtoCSV [-List | -ListIndex | -ListGroupInfo | -ListOccurrenceInfo | -ListOccurrences | -ListMessages] [fileName.mdrf] ...

    Also accepts alternates -io, -ie, -s, -nd, -hado, -mb, -i:interval, -s:dt, -e:dt, -g:name, -t:tag, -l, -li, -lgi, -loi, -lo, and -lm
```

The command line options for frequently used items are described as follows:

| Option (short version) | Description |
|:--|:--|
| -IncludeOccurrences (-io) | resulting columnar CSV output data is interspersed with occurrence records |
| -IncludeExtras (-ie) | requests the extraction to include extra information about the internal structure of each corresponding MDRF file and its constituent records as they are extracted.  |
| -Sparse (-s) | In columnar output data, this option causes the output to use an empty cell (,,) whenever the corresponding point was omitted from the current MDRF sparse group record (due to no change in value).  Without this option the output rows repeat prior column's point data when the MDRF file's contents are sparse. |
| -NoData (-nd) | blocks inclusion of actual rows of columnar data in resulting output file(s). |
| -HeaderAndDataOnly (-hado) | selects that the output csv file should only have a header row (names of columns) and data.  Simplifies importing into certain programs. |
| -MapBool (-mb) | selects that boolean values (True/False) shall be recorded in the csv output as an integer (1/0).  Simplifies charting with certain programs such as Excel. |
| -interval:interval (-i:interval) | sets the nominal per line interval (as fractional seconds) that the extraction tool will attempt to produce. |
| -start:deltaTime (-s:detlaTime) | sets the delta time (as fractional seconds) for where in each MDRF file the extraction should begin.  Generally this only used with individual files. |
| -end:deltaTime (-e:deltaTime) | sets the delta time (as fractional seconds) for where in each MDRF file the extraction should end.  Generally this is only used with individual files. |
| -tail:period | sets the extraction time range for an MDRF file to the last period seconds of the file.  Generally this is only used with individual files. |
| -group:name (-g:name) | Selects that the output shall only include groups that contain the given name as a substring.  This option may be repeated to select additional groups.  When omitted, all groups are selected by default. |
| -tag:tag (-t:tag) | Selects that the resulting CSV file names should be derived from the MDRF file name they are extracted from with the given tag string appended to their name. (a.mdrf => a_tag.csv).  This is generally used with batch files so that the resulting sets of extracted CSV files do not overwrite each other. |
| -List (-l) | Places the extraction application in List mode.  Prints basic summary information about each indicated MDRF file to standard output (the Console) |
| -ListIndex (-li) | Places the extraction application in List mode.  Prints detailed information about the contents of each MDRF file's index. |
| -ListGroupInfo (-lgi) | Places the extraction application in List mode.   Prints additional information about each group that has been defined in each MDRF file. |
| -ListOccurrenceInfo (-loi) | Places the extraction application in List mode.  Prints additional information about each occurrence that has been defined in each MDRF file. |
| -ListOccurrences (-lo) | Lists the occurrences found in the corresponding MDRF file(s) to the console. |
| -ListMessages (-lm) | Lists the messages found in the corresponding MDRF file(s) to the console. |

### Example batch files:

The sections below give an example batch file.

#### List.bat

```
..\..\ExtractMDRFtoCSV\ExtractMDRFtoCSV -l *.mdrf
pause
```

This batch file causes the extraction program to open and print summary information about each mdrf 
file that is found in the current directory.

## Terms

| Term | Description |
|:--|:--|
| MDRF | Mosaic Data Recording Format: This is a binary data recording file format that supports long term, variable rate, recording, and efficient replay, of both columnar and occurrence type information.  |
| CSV | Comma Separated Value file.  Generally suitable for direct import into tools such as Excel.  This file produces CSV output escaped per RFC4180 [https://tools.ietf.org/html/rfc4180] |

## End of Document