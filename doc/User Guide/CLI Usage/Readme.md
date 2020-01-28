# CLI Usage

Although this chapter primarily targets users, developers will likely find it helpful as well. The purpose of this document is twofold; first, to familiarize you with the built-in features of the OpenTAP CLI, and get started installing Keysight- and community developed plugins. Second, to introduce you to useful tools in constructing and managing test plans. 

Since a large chunk of the value of OpenTAP as a test automation platform comes from its extensibility through plugins, the application itself only ships with a few essential components:

1. a package manager to browse and install plugins; and
2. the capability to execute test plans;


## Using the package manager

### install
The `install` commands installs one or more packages.

`tap package install <package name> [<args>]`

In order to check what packages are available, run `tap package list`. To see what versions are available of a package, such as OpenTAP itself for instance, try `tap package list OpenTAP`.
This doesn't tell you much about the packages though. Have a look at [our repository](http://packages.opentap.io/index.html#/?name=OpenTAP) for a full description, and other information, about packages.

By default, the `install` action installs the latest stable release for your platform. Updating any package, including OpenTAP itself, is easy. Just run `tap package install OpenTAP`. Conversely, installing a specific version of any package is also simple. `tap package install OpenTAP --version 9.5.1` installs version 9.5.1; `--version beta` installs the latest beta; `--version rc` installs the latest release candidate. Note, however, that the package manager does not allow you to break dependencies. If you really know what you're doing, you can use the `--force` option to override this behavior.

New plugins may provide their own CLI actions, thus drastically increase the number of options. Luckily, OpenTAP keeps track of installed plugins for you, so you can always verify available CLI actions by running `tap`. Example output for a clean install (version 9.5.1):
```
$ tap

OpenTAP Command Line Interface (9.5.1)
Usage: tap <command> [<subcommand>] [<args>]

Valid commands are:
  run                   Runs a Test Plan.
  package
    create                Creates a package based on an XML description file.
    download              Downloads one or more packages.
    install               Install one or more packages.
    list                  List installed packages.
    test                  Runs tests on one or more packages.
    uninstall             Uninstall one or more packages.
    verify                Verifies installed packages by checking their hashes.
  sdk
    gitversion            Calculates a semantic version number for a specific git commit.

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.specific command.
```



## Running test plans

If you have some test plans of your own lying around, feel free to use them. Otherwise, you can install the [Demonstration](http://packages.opentap.io/index.html#/?name=Demonstration) plugin, which comes with a few mock devices and test plans preconfigured. Simply run `tap package install Demonstration -y`.

The `run` commands executes a test plan.

`tap run <file path> [<args>]`

This concludes the CLI portion of the guide. For information regarding the `sdk` subcommand, please see [this section](link) of the Developer Guide.

### External settings

Step settings can be marked as "External". This means they can be set 
from the CLI, or from a file. To see what external steps a test plan 
contains, try `tap run My.TapPlan --list-external`. 

If `--list-external` outputs:
```
TestPlan: My
Listing 3 external test plan parameters.
      value1 = x
      value2 = y
      value3 = z
```
then you can then set these values from the command line with `tap run My.TapPlan -e value1 hello -e value2 3 -e value3 0.75`.
Alternatively, you can create a csv file with the contents
```
value1,hello
value2,3
value3,0.75
```
Let's call the file "values.csv". You can then load these values into the external parameters with `tap run My.TapPlan -e values.csv`.
This makes it possible to reuse the same test plan for a variety

### Metadata

Analogous to external settings, resources settings can be marked as "Metadata". This could be 
the address of a DUT, for instance. Set this with `tap run My.TapPlan 
--metadata dut1=123 --metadata dut2=456`.
