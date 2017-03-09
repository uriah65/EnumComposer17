//------------------------------------------------------------------------------
// <copyright file="EnumComposerCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnumComposer;
using EnvDTE;
using EnvDTE80;

namespace EnumComposerVSIX
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class EnumComposerCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("5b163894-4531-428f-a2ff-fef516d013b2");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumComposerCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private EnumComposerCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static EnumComposerCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new EnumComposerCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            //string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            //string title = "EnumComposerCommand";

            //// Show a message box to prove we were here
            //VsShellUtilities.ShowMessageBox(
            //    this.ServiceProvider,
            //    message,
            //    title,
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            IEnumLog log = new EnumLog(outWindow);

            log.WriteLine("");
            log.WriteLine("started.");
            RunComposerScan(log);
            log.WriteLine("finished.");
        }

        #region 
        private void RunComposerScan(IEnumLog log)
        {
            try
            {
                RunComposerScan_Inner(log);
            }
            catch (Exception ex)
            {
                string message = "Sorry, and exception has occurred." + Environment.NewLine + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine + "See the Output\\Debug window for details.";
                if (log != null)
                {
                    string logMessage = DedbugLog.ExceptionMessage(ex);
                    log.WriteLine(logMessage);
                }

                //IVsUIShell uiShell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell)); //todo: new dialog
                //Guid clsid = Guid.Empty;
                //int result;
                //uiShell.ShowMessageBox(0,
                //       ref clsid,
                //       "EnumComposer Visual Studio Package",
                //       message,
                //       string.Empty,
                //       0,
                //       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                //       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                //       OLEMSGICON.OLEMSGICON_INFO,
                //       0,        // false
                //       out result);

                //string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
                string title = "EnumComposerCommand";

                // Show a message box to prove we were here
                VsShellUtilities.ShowMessageBox(
                    this.ServiceProvider,
                    message,
                    title,
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void RunComposerScan_Inner(IEnumLog log)
        {
            DTE2 applicationObject = (DTE2)Package.GetGlobalService(typeof(EnvDTE.DTE));

            TextDocument document = ObtainActiveDocument(applicationObject);
            if (document == null)
            {
                log.WriteLine("not a C# file.");
                return;
            }

            DbReader dbReader = new DbReader(null, null, log);
            IEnumConfigReader configReaderVsp = new ConfigReaderVsp(applicationObject.ActiveDocument.ProjectItem.ContainingProject);
            dbReader._configReader = configReaderVsp;

            ComposerStrings composer = new ComposerStrings(dbReader, log);
            ApplyComposer(document, composer);
        }

        public void ApplyComposer(TextDocument document, ComposerStrings composer)
        {
            /* get document bounds */
            EditPoint startEdit = document.CreateEditPoint(document.StartPoint);
            EditPoint endEdit = document.EndPoint.CreateEditPoint();

            /* run composer */
            string text = startEdit.GetText(document.EndPoint);
            composer.Compose(text);
            if (composer.EnumModels != null && composer.EnumModels.Count > 0)
            {
                /* get new file*/
                text = composer.GetResultFile();

                /* delete and re-insert full document */
                startEdit.Delete(endEdit);
                startEdit.Insert(text);
            }
        }

        private TextDocument ObtainActiveDocument(DTE2 applicationObject)
        {
            try
            {
                /* query ActiveDocument can cause exception if active document f.e. is project properties */
                if (applicationObject.ActiveDocument == null)
                {
                    return null;
                }

                TextDocument document = (TextDocument)applicationObject.ActiveDocument.Object("TextDocument");
                return document;
            }
            catch
            {
                /* see notes in try{} */
                return null;
            }
        }

        public string Reverse(string text)
        {
            /* test method, not used */
            char[] cArray = text.ToCharArray();
            string reverse = "";
            for (int i = cArray.Length - 1; i > -1; i--)
            {
                reverse += cArray[i];
            }
            return reverse;
        }

        #endregion 
    }
}
