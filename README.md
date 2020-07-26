# Quest Package Manager

A package manager for making Quest il2cpp mods and libraries

## Vocabulary

- `package`: An application with a single configuration. Can contain dependencies and has some metadata. Must have an id and a version (which must be SemVer).
- `dependency`: A dependency to another package. Must have an id and a version range (SemVer range).
- `sharedDir`: A folder that is exposed to other packages
- `externDir`: A folder that is used for installing dependencies

## Simple Guide

**Note: You can use `qpm`, `qpm -?`, `qpm --help`, or `qpm -h` to view a list of commands.**

### Creating a package

```bash
qpm pacakge create "PACKAGE ID" "PACKAGE VERSION"
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

### Restoring from a config file

After adding a `dependency`, you must perform a `restore` to properly obtain the `dependency`. Dependencies that are added to your `package` must be resolved in order to ensure a proper build.

Dependencies are resolved using an external domain, `qpackages.com`. For more information on how to interface with this package index, see the publishing section.

```bash
qpm restore
```

Which restores all dependencies listed in your package from the package index.

Assuming you have dependencies, the following process happens:

1. Searches the package index (`qpackages.com`) for any matching ids and versions
2. Collects all sub dependencies recursively.
3. Downloads the data from the url in the found package config, caches it in a temporary location (ApplicationData/QPM)
4. Copies over the `sharedDir` from the dependent package to your package's `externDir`
5. Downloads a .so file from the dependent package's `soLink` (if it is not `headersOnly`), caches it in a temporary location (ApplicationData/QPM)
6. Performs modifications to any local `Android.mk`, `.vscode/c_cpp_properties.json`, and `bmbfmod.json` files you have
7. Adds the dependency added to a local `qpm.lock.json`, which holds information about dependencies that have already been resolved.

At this point, you should be able to build!

### Publishing

Bother Sc2ad#8836 on Discord and tell them that you read this `README` and want to publish a package.

### Important notes for Beat Saber development

For beat saber development, it is important to ensure several things:

1. Your dependencies are valid semver ranges that will match your code. For this purpose, I strongly suggest using `^x.x.x` for any core libraries you may depend on. ex: `qpm dependency add "beatsaber-hook" -v "^0.3.0"`
2. `soLink` and `branchName` are strongly suggested additional properties. See `qpm properties-list` for a list of supported additional properties. Currently there is no way outside of pure edits to `qpm.json` to add additional data. It is planned in the future.
3. **IMPORTANT**: In order to build libraries that rely on `beatsaber-hook`, modify the dependency additional data for `beatsaber-hook` to contain the following (this should be done BEFORE `qpm restore`):

    ```json
    "extraFiles": [
        "src/inline-hook"
    ]
    ```

4. You will also need to modify these headers after a `qpm restore`: Specifically, `extern/beatsaber-hook/src/inline-hook/*` should now include: `#include "beatsaber-hook/utils/logging.hpp"` and `#include "beatsaber-hook/inline-hook/And64InlineHook.hpp"`