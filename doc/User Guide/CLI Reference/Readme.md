| Command                                                         | Description                                                                                              |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `tap`                                                           | Lists valid commands. Will also list new commands when new plugins are installed                         |
| `tap <command>`                                                 | Lists valid subcommands for `<command>`                                                                  |
| `tap <command> -h`                                              | Show help information for `<command>`                                                                    |
|                                                                 |                                                                                                          |
| `tap package <command>`                                         | Please note that package names are case sensitive, and certain characters such as spaces must be escaped |
| `tap package create <package xml>`                              | Create a tap package, or plugin, from an [XML description]()                                             |
| `tap package download`                                          | Download `<package>` to `%TAP_PATH%`                                                                     |
| `tap package install <package 1> [<package 2> … <package n>]`   | Install one or more packages. Specify repository with `-r <repo>`. Install dependencies with `-y`        |
| `tap package test <package>`                                    | Run the `test` actionstep of `<package>`                                                                 |
| `tap package uninstall <package 1> [<package 2> … <package n>]` | Uninstall one or more packages. Specify location with `-t <dir>`                                         |
| `tap package verify <package>`                                  | Verify the integrity of installed packages by comparing hashes.                                          |
|                                                                 |                                                                                                          |
| `tap run <testplan>`                                            | Run `<testplan>` -- all log output except `Debug` sent to stdout                                         |
| `tap run <testplan> --verbose`                                  | Run `<testplan>` -- all log output sent to stdout                                                        |
| `tap run <testplan> --settings`                                 |                                                                                                          |
| `tap run <testplan> --search`                                   |                                                                                                          |
| `tap run <testplan> -- metadata`                                |                                                                                                          |
| `tap run <testplan> --non-interactive`                          |                                                                                                          |
| `tap run <testplan> --external`                                 |                                                                                                          |
| `tap run <testplan> --try-external`                             |                                                                                                          |
| `tap run <testplan> --list-external-parameters`                 |                                                                                                          |
| `tap run <testplan> --results`                                  |                                                                                                          |
