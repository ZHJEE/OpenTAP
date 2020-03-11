# Editors

A test plan is composed of [XML elements](https://www.w3.org/XML/). The hierarchy of steps in a test plan is precisely
the hierarchy of XML elements. You can modify and extend test plans by hand, but making larger changes can quickly
become cumbersome. We therefore recommend using the tools that we are actively maintaining.

You currently have two options:
1. [Developer’s System Community Edition](https://www.opentap.io/download.html) - mature, feature-rich GUI (stable,
   recommended) - Windows only
2. [TUI](https://gitlab.com/OpenTAP/Plugins/opentap-tui/opentap-tui) - open source text-based user
   interface for usage in terminals (beta) - cross-platform 
   
This section is intended to help you pick the right editor for your task. Both editors provide a safe way to add and
remove test steps, as well as configuring options on individual test steps.

<!-- Editing test plans, providing a clear overview of what steps are available, safely making changes, -->
<!-- running testplans, clearly organizing the output of a testplan. E.g. -->
<!-- 1. Provide a clear overview of what steps passed, and what steps failed -->
<!-- 2. Display log output at various points throughout a run -->
<!-- 3. Breaking down the data associated with a test run in a way that empowers the user to analyze the results (results viewer) -->

## Developer’s System Community Edition

> The Community Edition is for non-commercial use. Commercial users, please see [KS8400A](https://www.keysight.com/en/pd-2747943-pn-KS8400A/test-automation-platform-developers-system)

This is the primary editor for test plans. It has been in development for many years, and integrates well with other
OpenTAP tools. Developer's System Community Edition is a bundle of packages
including the  Package Manager, the Results Viewer, the Run Explorer, the Timing Analyzer, Result
Listeners, and more.

Install the editor with the following command:

> tap package install "Developer's System CE"

You can now start the editor from the command line with `tap editor`, and it should show up in your start menu.
The editor ships with built-in documentation which can be accessed and searched by pressing F1.

## TUI

As mentioned in the previous section, `tap package install` installs the latest stable version. Since TUI is still in
beta, there is no stable version. Use the following command to install the latest available version:

> tap package install TUI --version any

and run the program with `tap tui`.


<!-- Result viewers -->
