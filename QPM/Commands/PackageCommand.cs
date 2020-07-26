using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("package", Description = "Package control")]
    [Subcommand(typeof(PackageCreate), typeof(PackageEdit))]
    internal class PackageCommand
    {
        [Command("create", Description = "Create a package")]
        internal class PackageCreate
        {
            [Argument(0, "id", "Id of the package to create")]
            [Required]
            public string Id { get; }

            [Argument(1, "version", "Version of the package to create")]
            [Required]
            public string Version { get; }

            [Option("-n|--name", CommandOptionType.SingleValue, Description = "Name of the package to create, defaults to id")]
            public string Name { get; }

            [Option("-u|--url", CommandOptionType.SingleValue, Description = "Url of the package to create, defaults to empty")]
            public string Url { get; }

            [Argument(2, "additional info", Description = "Additional information for the package (as a valid json object)")]
            public string AdditionalInfo { get; }

            // This will throw if it fails to do anything (ex: on invalid name and whatnot)
            // We may want to make this a bit clearer
            private void OnExecute()
            {
                if (string.IsNullOrEmpty(Id))
                    throw new ArgumentException("Id for 'package create' cannot be null or empty!");
                // Create PackageInfo
                // Throws on failure to create version
                var info = new PackageInfo(string.IsNullOrEmpty(Name) ? Id : Name, Id, new SemVer.Version(Version));
                if (!string.IsNullOrEmpty(Url))
                    // Throws on failure to create valid Uri
                    info.Url = new Uri(Url);
                // Populate AdditionalInfo
                if (AdditionalInfo != null)
                {
                    // TODO: Figure out this
                    Console.WriteLine(AdditionalInfo);
                    using var doc = JsonDocument.Parse(AdditionalInfo);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        throw new ArgumentException("AdditionalData must be a JSON object!");
                    foreach (var p in doc.RootElement.EnumerateObject())
                        info.AdditionalData.Add(p.Name, p.Value);
                }
                // Call package handler create
                Program.PackageHandler.CreatePackage(info);
                Console.WriteLine($"Created package: {Id} name: {info.Name} ok!");
                Utils.WriteSuccess();
            }
        }

        [Command("edit", Description = "Edit various properties of the package")]
        [Subcommand(typeof(PackageEditId), typeof(PackageEditName), typeof(PackageEditUrl), typeof(PackageEditVersion))]
        internal class PackageEdit
        {
            [Command("version", Description = "Edit the version property of the package")]
            internal class PackageEditVersion
            {
                [Argument(0, "version", Description = "Version to set the package version to")]
                [Required]
                public string Version { get; }

                private void OnExecute()
                {
                    // Create updated version
                    // Throws on failure
                    var newVersion = new SemVer.Version(Version);
                    // Call package handler change version
                    Program.PackageHandler.ChangeVersion(newVersion);
                    Console.WriteLine($"Changed version of package to: {newVersion} ok!");
                    Utils.WriteSuccess();
                }
            }

            [Command("id", Description = "Edit the id property of the package")]
            internal class PackageEditId
            {
                [Argument(0, "id", Description = "Id to set the package id to")]
                [Required]
                public string Id { get; }

                private void OnExecute()
                {
                    if (string.IsNullOrEmpty(Id))
                        throw new ArgumentException("Id for 'package edit id' cannot be null or empty!");
                    Program.PackageHandler.ChangeId(Id);
                    Console.WriteLine($"Changed id of package to: {Id} ok!");
                    Utils.WriteSuccess();
                }
            }

            [Command("url", Description = "Edit the url property of the package")]
            internal class PackageEditUrl
            {
                [Argument(0, "url", Description = "Url to set the package url to")]
                [Required]
                public string Url { get; }

                private void OnExecute()
                {
                    // Create updated url
                    // Throws on failure
                    var newUrl = new Uri(Url);
                    // Call package handler change url
                    Program.PackageHandler.ChangeUrl(newUrl);
                    Console.WriteLine($"Changed url of package to: {newUrl} ok!");
                    Utils.WriteSuccess();
                }
            }

            [Command("name", Description = "Edit the name property of the package")]
            internal class PackageEditName
            {
                [Argument(0, "name", Description = "Name to set the package name to")]
                [Required]
                public string Name { get; }

                private void OnExecute()
                {
                    if (string.IsNullOrEmpty(Name))
                        throw new ArgumentException("Id for 'package edit name' cannot be null or empty!");
                    Program.PackageHandler.ChangeName(Name);
                    Console.WriteLine($"Changed name of package to: {Name} ok!");
                    Utils.WriteSuccess();
                }
            }

            // TODO: Add qpm package edit extra (add, remove)

            private void OnExecute(CommandLineApplication app) => app.ShowHelp();
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}