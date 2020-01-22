# CLI Usage

As previously explained, a barebones OpenTAP installation only ships with a few commands. This section covers common usage of these commands. if this is not what you're looking for, check out the [comprehensive reference](../CLI%20Reference/) of built-in CLI options.


## Using the package manager

### install
The `install` commands installs one or more packages.

`tap package install <package name> [<args>]`

> Note: Updating OpenTAP is simple. Just run `tap package install OpenTAP`

New plugins may provide their own CLI actions, thus drastically increase the number of options. Luckily, OpenTAP keeps track of installed plugins for you, so you can always verify available CLI actions by running `tap`. Example output for a clean install (version 9.5.1):
```
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