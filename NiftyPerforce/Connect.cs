using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;

namespace Aurora
{

	namespace NiftyPerforce
	{
		// Main stub that interfaces towards Visual Studio.
		public class Connect : IDTExtensibility2, IDTCommandTarget
		{
			public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom) { }
			public void OnAddInsUpdate(ref Array custom) { }
			public void OnStartupComplete(ref Array custom) { }

			private Plugin m_plugin = null;

			public Connect()
			{
			}

			public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst_, ref Array custom)
			{
				AddIn addIn = (AddIn)addInInst_;
				m_plugin = new Plugin((DTE2)application, "NiftyPerforce");

				m_plugin.RegisterCommand(addIn, "P4Edit", "", "", "Opens the current document for edit", new Plugin.OnCommandFunction(new P4Edit().OnCommand));
				m_plugin.RegisterCommand(addIn, "P4EditItem", "Item", "P4 edit", "Opens the document for edit", new Plugin.OnCommandFunction(new P4EditItem().OnCommand));
				m_plugin.RegisterCommand(addIn, "P4RenameItem", "Item", "P4 rename", "Renames the item", new Plugin.OnCommandFunction(new P4RenameItem().OnCommand));
				m_plugin.RegisterCommand(addIn, "P4EditProject", "Project", "P4 edit", "Opens the project for edit", new Plugin.OnCommandFunction(new P4EditProject().OnCommand));
				m_plugin.RegisterCommand(addIn, "P4EditSolution", "Solution", "P4 edit", "Opens the solution for edit", new Plugin.OnCommandFunction(new P4EditSolution().OnCommand));
				m_plugin.RegisterCommand(addIn, "Configuration", "", "", "Opens the configuration dialog", new Plugin.OnCommandFunction(new NiftyConfigure().OnCommand));
			}

			public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
			{
				if (neededText != vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
					return;

				if (!m_plugin.CanHandleCommand(commandName))
					return;

				status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
			}

			public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
			{
				handled = false;
				if (executeOption != vsCommandExecOption.vsCommandExecOptionDoDefault)
					return;

				handled = m_plugin.OnCommand(commandName);
			}

			public void OnBeginShutdown(ref Array custom)
			{
				//TODO: Make this thing unregister all the callbacks we've just made... gahhh... C# and destructors... 
			}
		}
	}

}
