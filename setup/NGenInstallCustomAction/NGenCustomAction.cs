// <copyright file="NGenCustomAction.cs" company="N/A">
// Copyright 2011 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

namespace NGenInstallCustomAction
{
    using System;
    using System.Collections;
    using System.Configuration.Install;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Permissions;

    /// <summary>
    /// Custom install action to run NGen.
    /// Source: http://msdn.microsoft.com/en-us/library/3hwzzhyd.aspx
    /// </summary>
    public class NGenCustomAction : Installer
    {
        /// <summary>
        /// Installs the specified saved state.
        /// </summary>
        /// <param name="savedState">State of the saved.</param>
        [SecurityPermission(SecurityAction.Demand)]
        public override void Install(IDictionary savedState)
        {
            base.Install(savedState);
            this.Context.LogMessage(">>>> ngenCA: install");
            this.ngenCA(savedState, "install");
        }

        /// <summary>
        /// When overridden in a derived class, completes the install transaction.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary"/> 
        /// that contains the state of the computer after all the installers in the
        /// collection have run.</param>
        /// <exception cref="T:System.ArgumentException">The <paramref name="savedState"/>
        /// parameter is null.-or- The saved-state 
        /// <see cref="T:System.Collections.IDictionary"/> might have been corrupted.
        /// </exception>
        /// <exception cref="T:System.Configuration.Install.InstallException">An
        /// exception occurred during the
        /// <see cref="M:System.Configuration.Install.Installer.Commit(System.Collections.IDictionary)"/> 
        /// phase of the installation. This exception is ignored and the 
        /// installation continues. However, the application might not function
        /// correctly after the installation is complete. </exception>
        [SecurityPermission(SecurityAction.Demand)]
        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);
            this.Context.LogMessage(">>>> ngenCA: commit");
        }

        /// <summary>
        /// When overridden in a derived class, removes an installation.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary"/> 
        /// that contains the state of the computer after the installation was complete.</param>
        /// <exception cref="T:System.ArgumentException">The saved-state 
        /// <see cref="T:System.Collections.IDictionary"/> might have been 
        /// corrupted. </exception>
        /// <exception cref="T:System.Configuration.Install.InstallException">
        /// An exception occurred while uninstalling. This exception is ignored 
        /// and the uninstall continues. However, the application might not be 
        /// fully uninstalled after the uninstallation completes. </exception>
        [SecurityPermission(SecurityAction.Demand)]
        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);
            this.Context.LogMessage(">>>> ngenCA: uninstall");
            this.ngenCA(savedState, "uninstall");
        }

        /// <summary>
        /// When overridden in a derived class, restores the pre-installation
        /// state of the computer.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary"/> 
        /// that contains the pre-installation state of the computer.</param>
        /// <exception cref="T:System.ArgumentException">The <paramref name="savedState"/>
        /// parameter is null.-or- The saved-state 
        /// <see cref="T:System.Collections.IDictionary"/> might have been
        /// corrupted. </exception>
        /// <exception cref="T:System.Configuration.Install.InstallException">
        /// An exception occurred during the 
        /// <see cref="M:System.Configuration.Install.Installer.Rollback(System.Collections.IDictionary)"/> 
        /// phase of the installation. This exception is ignored and the rollback 
        /// continues. However, the computer might not be fully reverted to its
        /// initial state after the rollback completes. </exception>
        [SecurityPermission(SecurityAction.Demand)]
        public override void Rollback(IDictionary savedState)
        {
            base.Rollback(savedState);
            this.Context.LogMessage(">>>> ngenCA: rollback");
            this.ngenCA(savedState, "uninstall");
        }

        /// <summary>
        /// Perfrom NGen.
        /// </summary>
        /// <param name="savedState">State of the saved.</param>
        /// <param name="ngenCommand">The ngen command.</param>
        [SecurityPermission(SecurityAction.Demand)]
        private void ngenCA(IDictionary savedState, string ngenCommand)
        {
            string[] argsArray;

            if (string.Compare(ngenCommand, "install", StringComparison.OrdinalIgnoreCase) == 0)
            {
                string args = Context.Parameters["Args"];
                if (string.IsNullOrEmpty(args))
                {
                    throw new InstallException("No arguments specified");
                }

                char[] separators = { ';' };
                argsArray = args.Split(separators);

                // It is Ok to 'ngen uninstall' assemblies which were not installed
                savedState.Add("NgenCAArgs", argsArray);
            }
            else
            {
                argsArray = (string[])savedState["NgenCAArgs"];
            }

            // Gets the path to the Framework directory.
            string frameworkPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

            for (int i = 0; i < argsArray.Length; ++i)
            {
                string arg = argsArray[i];

                // Quotes the argument, in case it has a space in it.
                arg = "\"" + arg + "\"";

                string command = ngenCommand + " " + arg;

                ProcessStartInfo si = new ProcessStartInfo(Path.Combine(frameworkPath, "ngen.exe"), command);
                si.WindowStyle = ProcessWindowStyle.Hidden;

                Process p;

                try
                {
                    Context.LogMessage(">>>>" + Path.Combine(frameworkPath, "ngen.exe ") + command);
                    p = Process.Start(si);
                    p.WaitForExit();
                }
                catch (Exception ex)
                {
                    throw new InstallException("Failed to ngen " + arg, ex);
                }
            }
        }
    }
}
