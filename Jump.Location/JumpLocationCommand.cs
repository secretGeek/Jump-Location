﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace Jump.Location
{
    [Cmdlet("Jump", "Location")]
    public class JumpLocationCommand : PSCmdlet
    {
        private static bool _hasRegisteredDirectoryHook;
        private static readonly CommandController Controller;

        static JumpLocationCommand()
        {
            var home = Environment.GetEnvironmentVariable("USERPROFILE");
            // TODO: I think there's potential here for a bug
            home = home ?? @"C:\";
            var dbLocation = Path.Combine(home, "jump-location.txt");
            Controller = CommandController.Create(dbLocation);
        }

        public static IEnumerable<string> GetTabExpansion(string searchTerm)
        {
            return Controller.GetMatchesForSearchTerm(searchTerm).Select(x => x.Path);
        }

        /*
         * x1. Figure out how long they stay in the directory
         * x2. Log occurences of filename / weight
         * x3. Tail matches - search matches beginning of last segment of path
         * x4. Make MSI installer for easy use
         * 5. Weighting algorithm - match what Autojump does to increase weights
         * 6. Match what Autojump does to degrade weights
         * x7. Multiple args - last arg is a tail match, previous args match previous segments
         * x8. Tab completion - list 5 best matches
         * x9. Get-JumpStat
         */

        [Parameter(ValueFromRemainingArguments = true)]
        public string[] Directory { get; set; }

        [Parameter]
        public SwitchParameter Status { get; set; }

        [Parameter]
        public SwitchParameter Initialize { get; set; }

        public static void UpdateTime(string location)
        {
            Controller.UpdateLocation(location);
        }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            if (_hasRegisteredDirectoryHook) return;

            InvokeCommand.InvokeScript(@"Set-PSBreakpoint -Variable pwd -Mode Write -Action {
                [Jump.Location.JumpLocationCommand]::UpdateTime($($(Get-Item -Path $(Get-Location))).PSPath);
            }");

            _hasRegisteredDirectoryHook = true;
        }
        
        protected override void ProcessRecord()
        {
            if (Status)
            {
                Controller.PrintStatus();
                return;
            }

            // This lets us do just `Jump-Location` to initialize everything in the profile script
            if (Initialize)
            {
                InvokeCommand.InvokeScript(@"
                    [Jump.Location.JumpLocationCommand]::UpdateTime($($(Get-Item -Path $(Get-Location))).PSPath);
                ");
                return;
            }

            if (Directory == null) return;

            // If it has a \ it's probably a full path, so just process it
            if (Directory.Length == 1 && Directory.First().Contains('\\'))
            {
                ChangeDirectory(Directory.First());
                return;
            }

            var best = Controller.FindBest(Directory);
            if (best == null) throw new LocationNotFoundException(Directory.First());

            var fullPath = best.Path;
            ChangeDirectory(fullPath);
        }

        private void ChangeDirectory(string fullPath)
        {
            InvokeCommand.InvokeScript(string.Format("Set-Location {0}", fullPath));
        }
    }
}
