Name:    OpenTAP
Version: $(GitVersion)
Release: $(GitVersion)

Summary: Simple, scalable, and fast test sequencing framework
License: MPLv2.0
URL: opentap.io
Source0: gitlab.com/opentap/opentap


%description
OpenTAP is an Open Source project for fast and easy development and execution 
of automated tests. OpenTAP is built with simplicity, scalability and speed in 
mind, and is based on an extendable architecture that leverages .NET Core. 

OpenTAP offers a range of sequencing functionality and infrastructure that 
makes it possible for you to quickly develop plugins tailored for your 
automation needs – plugins that can be shared with the OpenTAP community 
through the OpenTAP package repository.

%prep
%setup
%build

%install
cp -rfa * %{buildroot}

%files
/*