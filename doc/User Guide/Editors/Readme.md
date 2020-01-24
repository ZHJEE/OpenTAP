# Editors

As you may have noticed, a test plan consists simply of XML. This language is easy for machines to read, but, unfortunately, horrible for humans to write. You *could* modify and extend test plans by hand, but we recommend using the tools we have developed.

You currently have two options:
1. [Developer’s System Community Edition](https://www.opentap.io/download.html) - mature, feature-rich GUI (recommended)
2. [TUI](https://gitlab.com/OpenTAP/Plugins/opentap-tui/opentap-tui) - open source cross-platform text-based user interface for usage in terminals (beta)

## Developer’s System Community Edition

Install the community edition version of the editor with the following command:

> tap package install "Editor CE"

and run it with `tap editor`.

## TUI

As mentioned in the previous section, `tap package install` installs the latest stable version. Since TUI is still in beta, there is not stable version.
Use the following command to install the latest version:
> tap package install TUI --version any

and run the program with `tap tui`.