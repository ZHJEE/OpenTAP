# Introduction

This document is intended to make new users familiar with the built-in features of the tap CLI, and get started installing Keysight- and comunity developed plugins.

Since a large chunk of the value of OpenTAP as a test automation platform comes from its extensibility through plugins,
the application itself only ships with a few essential components:

1. the capability to execute test plans;
2. a package manager to browse and install plugins;
3. sdk tools to develop new plugins.

OpenTAP keeps track of installed plugins, so you can always verify available CLI actions by simply running `tap --help`.
Click [here](../CLI%20Reference/) for a comprehensive reference of built-in CLI options.

## Common commands

Usage: `tap <command> [<subcommand>] [<args>]`

### run

The `run` commands executes a test plan.

`tap run <file path> [<args>]`

### package install

The `install` commands installs one or more packages.

`tap package install <package name> [<args>]`

> Note: Upgrading OpenTAP is simple, just run `tap package install OpenTAP`

### sdk new

OpenTAP includes tools to generate new projects, project files (e.g. new TestStep or Instrument) and integration with other tools (e.g. VSCode or GitLab).

> Note: This command uses the sdk package that can be install with `tap package install SDK`.

`tap sdk new <command> [<subcommand>] [<args>]`

List valid subcommands with: `tap sdk new`.

> Note: Create a new plugin project with `tap sdk new project <project name>`
