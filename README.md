# KSP2 Redux SDK Manager

This is a manager for your KSP2 Redux Mod projects, meant to make creating new projects and upgrading them
only a few intuitive button presses :3

It comes with 2 different parts, a CLI, and a GUI

## GUI

The GUI is an interactive application which will list all your projects and known SDK versions, with buttons for
adding/creating projects easily.

## CLI

The CLI is a command line interface that does the same stuff as the GUI for those with technical know-how, it can be
used as follows:

```
> redux-sdk-cli help
  versions    List the template versions available in the distribution repo.

  create      Create a new project from a template version into an empty
              directory.

  ingest      Adopt an existing pre-manager project and bring it to a template
              version.

  upgrade     Upgrade a managed project to a template version.

  import      Register an already-managed project (has template.version) with
              the manager, unchanged.

  detect      Report the template version a project is stamped with.

  open        Open a project in its Unity editor, offering to install it via
              Unity Hub if missing.

  unity       List the Unity editors installed via Unity Hub.

  projects    List the projects the manager is tracking.

  doctor      Check that git and Unity Hub are available.

  help        Display more information on a specific command.

  version     Display version information.
```