# Quest Package Manager

A package manager for making Quest il2cpp mods and libraries. Commonly acronymized as `QPM` or `qpm`

## Vocabulary

- `package`: An application with a single configuration. Can contain dependencies and has some metadata. Must have an id and a version (which must be SemVer).
- `dependency`: A dependency to another package. Must have an id and a version range (SemVer range).
- `sharedDir`: A folder that is exposed to other packages
- `externDir`: A folder that is used for installing dependencies

## Simple Guide

**Note: You can use `qpm`, `qpm -?`, `qpm --help`, or `qpm -h` to view a list of commands.**

### Creating a package

```bash
qpm package create "PACKAGE ID" "PACKAGE VERSION"
```

Creates a package with id: `PACKAGE ID` and version: `PACKAGE VERSION` (which must be valid SemVer).

This will create a `qpm.json` folder in your current directory. This is your package configuration file and holds your `package`.

This will also perform some modifications to your `.vscode/c_cpp_properties.json`, `bmbfmod.json`, and `Android.mk` files, assuming they exist.

### Adding a dependency

A common use case with `qpm` is to add a dependency to a package.

You must first have a valid `qpm.json` file within your current working directory, then you may call:

```bash
qpm dependency add "ID" -v "VERSION RANGE"
```

Which creates a dependency with id: `ID` and version range: `VERSION RANGE`. If `-v` is not specified, version range defaults to `*` (latest available version)

### Collect Dependencies

This is primarily a command used to ensure the package's collected dependencies match what you expect.

```bash
qpm collect
```

Should print out a listing of dependencies that it resolved. This command does not modify your package at all.

### Collapse Dependencies

This is primarily a command used to ensure the package's collected dependencies are collapsible into something you expect.

```bash
qpm collapse
```

Should print out a listing of dependencies similar to [Collect Dependencies](#collect-dependencies), but with identical IDs combined.

### Restoring Dependencies

After adding one or more `dependencies`, you must perform a `restore` to properly obtain them. Dependencies that are added to your `package` must be resolved in order to ensure a proper build.

Dependencies are resolved using an external domain, `qpackages.com`. For more information on how to interface with this package index, see the publishing section.

```bash
qpm restore
```

Which restores all dependencies listed in your package from the package index.

Restore follows the following process:

1. Collects all dependencies
2. Collapses these dependencies
3. Iterates over the collected dependencies, obtaining a .so (if needed) for each one.
4. Iterates over the collapsed dependencies, obtaining header information (if needed) for each one.
5. Places all restored dependencies into `qpm.shared.json` (which is used for publishing)

During steps 3 and 4, QPM will modify your `Android.mk`, if it exists, backing it up to `Android.mk.backup` first.

At this point, you should be able to build!

### Publishing

There are several steps required in order to ensure your package is fit to be published on `qpackages.com`.

Firstly, you must perform a `qpm restore` (see [Restoring Dependencies](#restoring-dependencies))

After, you must ensure you either have the `headersOnly` additional property set to true, or you specify `soLink` and/or `debugSoLink`.

Then, the url must be set to either a github repository (ex: `https://github.com/sc2ad/QuestPackageManager`) or will be interpretted as a direct download.

The download specified by this link **MUST** have a `qpm.json` file that matches the version specified in your `qpm.shared.json` that you plan on publishing.

After you have ensured that all of the above is true, you can call:

```bash
qpm publish
```

However, due to the nature of QPM, it is possible to publish invalid packages to QPM, which will cause people trouble.
For this reason, please message `Sc2ad#8836` before publishing a package to QPM.

### Extra Notes

`qpm package edit-extra` Can be used to add extra data to the `additionalData` property, without manual JSON edits to `qpm.json` and `qpm.shared.json`.

`qpm properties-list` Can be used to list all supported properties in `additionalData`, as well as what types they are supported in.

A full list will be available on the wiki, once I get around to making it.

Full documentation for each command will be fully available on the wiki (once I get around to making it). 
A subset of this information can be found by doing `qpm --help`.

**`qpm.shared.json`, `Android.mk.backup`, and `extern` should never be added to a git project!**

You should explicitly add:

```yml
qpm.shared.json
extern/
*.backup
```

to your `.gitignore`.

### Updating a dependency

As easy as:

```bash
qpm dependency add "<DEPENDENCY ID>" -v "<NEW DEPENDENCY VERSION>"
```

followed by:

```bash
qpm restore
```

### QPM Cache

QPM caches all restored dependencies to: `<QPM WORKING DIRECTORY>/QPM_Temp/`

You can forcibly clear the QPM cache by calling:

```bash
qpm cache clear
```

## Beat Saber Development

QPM was built with Beat Saber Quest development in mind.
This does not mean it does not work on other games, or for other platforms, but it makes Beat Saber development very easy.

In order to get started, it is recommended you have a VSCode project open, with existing `.vscode/c_cpp_properties.json`, `./Android.mk`, and `./bmbfmod.json` files.

**NOTE: Using the bsqm template may cause issues, since you will need to delete your `extern` folder and your `.gitmodules` file before continuing!**

Then, you should start by creating your QPM package. This can be done by calling:
```bash
qpm package create <YOUR MOD ID> <YOUR VERSION>
```

After this, you should add a dependency to beatsaber-hook.
It is good practice to specify an encompassing version range for your dependencies, so `^x.y.z` where `x.y.z` is the latest `beatsaber-hook` version.

This can be found by checking the latest published package by visiting this link: [https://qpackages.com/beatsaber-hook/](https://qpackages.com/beatsaber-hook/).

Once you know the version range you would like, call:

```bash
qpm dependency add "beatsaber-hook" -v "<VERSION RANGE>"
```

In most cases, this would be all we need to do before calling a `qpm restore` and building our mod.
SADLY, `beatsaber-hook` has an issue which requires us to take one additional step. We need to edit `qpm.json` and edit the dependency from:

```json
"dependencies": [
    {
        "id": "beatsaber-hook",
        "versionRange": "<VERSION RANGE>",
        "additionalData": {}
    }
]
```

to:

```json
"dependencies": [
    {
        "id": "beatsaber-hook",
        "versionRange": "<VERSION RANGE>",
        "additionalData": {
            "extraFiles": [
                "src/inline-hook"
            ]
        }
    }
]
```

_NOW_ we can perform a:

```bash
qpm restore
```

Finally, we just need to add a few lines to our `Android.mk` and we will be all set!

For your main module's `LOCAL_CFLAGS` or `LOCAL_CPP_FLAGS`, add the following flag: `-isystem"./extern/libil2cpp/il2cpp/libil2cpp"`. This adds `libil2cpp` from your `extern` folder.

For the `beatsaber-hook_x_y_z` module's `LOCAL_EXPORT_C_FLAGS`, add the following flags: `-DNEED_UNSAFE_CSHARP -DUNITY_2019`

Add to your main module the following (assuming you have `wildcard` defined):

```mk
LOCAL_SRC_FILES += $(call rwildcard,extern/beatsaber-hook/src/inline-hook,*.cpp)
LOCAL_SRC_FILES += $(call rwildcard,extern/beatsaber-hook/src/inline-hook,*.c)
```

For intellisense, add `${workspaceFolder}/extern/libil2cpp/il2cpp/libil2cpp` to your `.vscode/c_cpp_properties.json`'s `includePath`.

Now you should be all set to build! (You won't need to make these modifications the next time you perform a `qpm restore`)

## Plugins

Coming soon, QPM will allow for plugins to allow for modifications of additional files, or perform additional actions for all commands.
QPM will also allow plugins to add commands as well.
Literally not for a long time though.

## Issues

DM `Sc2ad#8836` on Discord or something, idk
