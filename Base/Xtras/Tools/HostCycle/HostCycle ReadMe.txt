# HostCycle

## Overview

This document is an initial readme file for the HostCycle test application.

This application is an early example or test application that makes partial use of I300I related semi standards
to support setup and use of a target equipment application.  This application currently includes the following basic functions:

* Configurable defintions for a subset of the Semi standard required E5, E30 and I300I VIDS.
* HSMS connection usage with configurable target and logging characteristics.
* Ability to perform basic E30 connetion operations - Disable/Enable, Offline/HostOnline, Local/Remove (via configurable S2F41 usage)
* Useful set of E30 and I300I CEID and report definition, linkage, usage and visualization.
* Support for basic use of E87, E94, E40, and E90 on PortID 1 including normal round trips and cycling using carrier recreate.
* Support for basic use of Terminal Services.
* SVID screen display with refresh and moderate rate scan
* ECID screen display with refresh and automatic tracking of changes in individual values using corresponding E30 CEID.
* ALID screen display with automatic tracking of state through corresponding E30/E5 stream/function (S5F1, ...)
* E39 treeview display with Refresh button and automatic sub-tree refresh based on selected corresponding events (by type and/or specific objectID in events)

## Configuration

Configuration of this application is based on the use of Modular.Config.  It currently has two sets of configuration sources:
  * App.Config
  * equipment specific version of Config/Empty_Include_VIDs.json

The current App.Config is used to configure logging infrastructure and defines the following HostCycle usage specific key values:

'''
    <add key="Config.HostCycle.HostName" value="localhost"/>
    <add key="Config.HostCycle.PortNum" value="5000"/>
    <add key="Config.HostCycle.DeviceID" value="32767"/>
    <add key="Config.HostCycle.HeaderTraceMesgType" value="Debug"/>
    <add key="Config.HostCycle.MesgTraceMesgType" value="Debug"/>
    <add key="Config.HostCycle.HighRateMesgTraceMesgType" value="Trace"/>
'''

Most of the keys used here are actually extracted and used within the E005Port and E005Manager classes and additional configurable behaviors may be available there.

The user is expected to copy, modify and fill in the given Empty_Include_VIDs.json file with all of the equipment specific values that are used here.
Typically the user modifies the app.config or the shortcut used to launch the HostCycle application by adding an Include key such as Include.File.VIDs=Config\Empty_Include_VIDs.json.
This is used by the standard include file provider (loaded as part of the AppSetup.HandleOnStartup call) to load the sub-key source file (Empty_Include_VIDS.json in this case) so that its keys are available to the application.
This is then used to configure the relevant portions of the HostCycle's host interface so that it knows the required set of IDs that are required to support basic E5, E30 and I300I capabilites that are used here.

## Notes

This application is in a very early phase in its overall development and may not function as expected for any particular purpose.  
It is likely that this application (along with corresponding writings such as this one) will evolve, and hopefully improve, significantly over time.

## End of document