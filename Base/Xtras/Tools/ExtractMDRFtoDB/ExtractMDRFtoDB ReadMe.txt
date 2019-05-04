# ExtractMDRFtoDB ReadMe

## Overview

This document is a readme which gives summary usage information about the ExtractMDRFtoDB tool.
This tool is a follow on, and eventual replacement, to the existing ExtractMDRFtoCSV tool.  
Its new behavior and capabiltites are intended to be more useful in data analytics workflows, both repeatitious and alacarte types of tasks.

Unlike the prior ExtractMDRFtoCSV, this tool includes the following primary improvements:

* Can consolidate data from more than one MDRF into a single output file so as to be able to reconstruct a single contiguous time range from a set of sequentially generated MDRF files.
* Supports table centric output file formats.  Initially this tool supports output to SQLite3 files.
* Supports extraction of Occurrence data into a MDRF_Occurrences table so that they may be used to better inform and index the extracted tabular data where appropriate.
* Supports use with .ini style configuration files to support a wide range of pre-defined tasks.
* Command line parsing has been adjusted to improve usefulness with drag and drop style of program useage.  Expected use patterns include dragging a single .ini file or dragging an .ini file and set of .mdrf files to the program (or a to a shortcut to it).

This tool supports the following filtering abilities when performing extraction tasks:

* optionally constrain the set of groups, points and/or occurrences that are included.
* optionally perform a simple set of name mappings on group names, occurrence names, and/or point names.
* optionally select a specific Date Range or TailPeriod (last 30 minutes for example) to be used for extraction.
* optionally specify a NominalSampleInterval to support cadenced sub-sampling based reduction of the original data.
 
Please note that this tool is based on the use of MosaicLib.Modular.Config for configuration of all aspects of its operation and use.  
This document describes the use of configuration .ini files for this purpose, however the tool is usable when configuration is done 
using equivilent configuration sources including the command line (key=value ...) and app.config, as well as the other default set 
of config key providers (EnvVars, Include).

## Overview of operation

This tool processes its command line and selected files in phases:

* Setup configuration and extract, process and remove key=value style arguments from the command line
* Process configuration .ini files from command line
* Optionally change working directory to the directory of the first configuration .ini file (enabled by default)
* Create/truncate and initialize the output data file
* Process MDRF file specifications from command line
* Read indicated MDRF files as configured
* Write the selected contents from these MDRF files to the output file.

## General operation

On launch the application processes the arguments given on the command line.  These are expected to include any of three patterns:
 
* INI files
* MDRF files
* parameter values (key=value)

The application processes all of the parameter values first through the use of '''Config.AddStandardProviders(ref args, StandardProviderSelect.All);''' 
which recognizes, removes, and processes any parameter value assignment items that are found in the user provided command line arguments.

Next the application extracts and processes all of the argments that look like the name of a ini file (names that end in .ini).  These are each loaded in 
turn using an IniFileConfigKeyProvider and processed.  Generally the program uses the key value from the last such ini file that specified that key,
however the following key(s) are handled specially:

* MDRFFileSpec - Each file that specifies a non-empty value for this string gets added to the effective set of MDRF file (and file search) strings.
* IncludeGroupSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* IncludeIOPointSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* IncludeIOPointPrefixSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* IncludeIOPointContainsSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* ExcludeIOPointContainsSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* IncludeOccurrenceSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* MapSpecStr - values from multiple configuration .ini files are combined when processing the MDRF files.
* AutoCDToIniFileDirectory - only applies to the first configuration .ini file that does not explicitly disable this option.

Once all of the ini files have been processed the application determines the desired type of data file and creates/truncates and initializes the
selected data output file that the MDRF file contents will be extracted into.

Then the application processes all of the remaining command line items as names of MDRF files or as search strings for names of MDRF files (*.MDRF for example).
For each such file it reads the file's contents using the selected filter criteria and then writes that file's contents into the output data file.

Depending on the selected output file format, writing to the output file is either done as the MDRF file contents are read or is batched together
after all MDRF file contents have been read into memory.

## configuration files: supported keys and usage

* MDRFFileSpec (string)
* AutoCDToIniFileDirectory (boolean)
* DataFileType (one of None, SQLite3)
* DateFileName (string)
* CreateMode (one of Append, Truncate) - defaults to Truncate.
* StartDateTime (string in supported System.DateTime parsable format)
* EndDateTime (string in supported System.DateTime parsable format)
* TailPeriod (string in supported System.TimeSpan parsable format) - Use of TailPeriod overwrites StartDateTime and EndDateTime.
* NominalSampleInterval (string in supported System.TimeSpan parsable format: either floating point seconds ss.fff or hh:mm:ss.fff)
* IncludeGroupSpecStr (string, items in comma seperated list)
* IncludeIOPointSpecStr (string, items in comma seperated list)
* IncludeIOPointPrefixSpecStr (string, items in comma seperated list)
* IncludeIOPointContainsSpecStr (string, items in comma seperated list)
* ExcludeIOPointContainsSpecStr (string, items in comma seperated list)
* IncludeOccurrenceSpecStr (string, items in comma seperated list)
* MapSpecStr (string, mapping items in comma seperated list.  Each mapping item is old:new)
* MaxThreadsToUse (int, when present this limits the number of concurrent threads used to load MDRF files.  It may be used to similarly limit the amount of memory that the application uses by limiting the number of MDRF file contents that must be able to be loaded into memory at one time to this value.)

## Filtering

The various configuration keys whose names begin with Include or Exclude are used to control which parts of an MDRF file will be included in the output file.
Such keys are provided for Groups, IOPoints, and Occurrences.

For Groups and Occurences there is only one IncludeYYYSpecStr key each.  These keys list the group/occurrence name(s) that are to be included.  When either such key is present then only those groups/occurrences who's names are explicitly listed will be included.
When either key is not included (contents are null or empty - the default)) then all of the corresponding set of names will be included.

For IOPoints there are four keys: IncludeIOPointSpecStr, IncludeIOPointPrefixSpecStr, IncludeIOPointContainsSpecStr and ExcludeIOPointContainsSpecStr.
All IOPoints for each group will be included by default if the first two keys are null/empty and the 3rd is or the 4th is not.  
Otherwise the first 2 are used to opt iopoint names in then the 4th to exclude them and then the 3rd to add them.  Thus ExcludeIOPointContainsSpecStr has priority over the first two include spec str keys but not the IncludeIOPointContainsSpecStr key.

Note: The IncludeIOPOintSpecStr key can specify point names directly or prefixed with the group name (and a period - as in group.point).

Groups that have no included points will not be included.

This asymetry in filtering is due to the relatively large number of individual IOPoint names and thus the desire to support filtering tools in addition to simple enumeration.

## Supported output formats

### SQLite3

When SQLite3 is selected as the output format, the application generates a set of fixed tables and a set of content dependent tables.

The following is the list of fixed tables:

* MDRF_Files - table of the files that have been added to the output data file
* MDRF_Messages - table of the MDRF writer generated messages that were generated while recording the MDRF files.
* MDRF_Errors - table of the MDRF writer generated error messsages that were generated while recording the MDRF files (usually empty).
* MDRF_Occurrences - table of the occurrence records that have been recorded and extracted.

The MDRF_Files table includes the following columns: Name TEXT, SizeBytes INT, StartDateTime TIMESTAMP, LastBlockDateTime TIMESTAMP, WasProperlyClosed INT, FileInfo TEXT, DateTimeInfo TEXT, OccurrencesInfo TEXT, GroupsInfo TEXT
The MDRF_Messages and MDRF_Errors tables include the following columns: DateTime TIMESTAMP, Text TEXT
The MDRF_Occurrences table includes the following columns: DateTime TIMESTAMP, Name TEXT, Value TEXT

In addition to these, the application generates a table per Group that has been included.  Each of these tables includes the following
default column: DateTime TIMESTAMP.  This is followed by a column per IOPoint in the corresponding group.  
Generally IOPoint value columns carry a type that is consistent with the first non-empty value observed for that IOPoint while reading the
first MDRF file that was processed.  These types are typically REAL, INT, TIMESTAMP, or TEXT.  Types that are not recognized will generally use the BLOB type.

In addition, for the MDRF_Occurrence's Value column, and for the IOPoint columns which contain non-simple value types, this application will
generally try to convert the given value to its simplest JSON content representation and then will save that value in the table.  This allows
recorded NamedValueSet, ListOfString, and array representations to be extracted into the output tables even though
the table does not have any appropriately similar column type.  JSON has been choosen due to the wide availability of
parsers for this representation and thus the hope that such complex types can be extracted and processed from the data files that are produced
by this application for these cases.

Please note that by default TIMESTAMP is an F8 representation of the DateTime using the Julian calendar (https://en.wikipedia.org/wiki/Julian_day)

## End of document