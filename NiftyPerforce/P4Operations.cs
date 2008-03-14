// Copyright (C) 2006-2007 Jim Tilander. See COPYING for and README for more details.
using System;
using EnvDTE;
using System.Threading;
using System.Collections.Generic;

namespace Aurora
{
	namespace NiftyPerforce
	{
		// Simplification wrapper around running perforce commands.
		class P4Operations
		{
			public static bool IntegrateFile(OutputWindowPane output, string filename, string oldName)
			{
                return ScheduleRunCommand(output, "p4.exe", GetUserInfoString() + "integrate \"" + oldName + "\" \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
			}

			public static bool DeleteFile(OutputWindowPane output, string filename)
			{
				return ScheduleRunCommand(output, "p4.exe", GetUserInfoString() + "delete \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
			}

			public static bool AddFile(OutputWindowPane output, string filename)
			{
				return ScheduleRunCommand(output, "p4.exe", GetUserInfoString() + "add \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
			}

			public static bool EditFile(OutputWindowPane output, string filename)
			{
				return ScheduleRunCommand(output, "p4.exe", GetUserInfoString() + "edit \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
			}

			public static bool EditFileImmediate(OutputWindowPane output, string filename)
			{
				return RunCommand(output, "p4.exe", GetUserInfoString() + "edit \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename), m_commandCount++);
			}

            public static bool RevertFile(OutputWindowPane output, string filename)
            {
				return ScheduleRunCommand(output, "p4.exe", GetUserInfoString() + "revert \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
            }

            public static bool DiffFile(OutputWindowPane output, string filename)
            {
				return ScheduleRunCommand(output, "p4win.exe", GetUserInfoString() + "-D \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
            }

            public static bool RevisionHistoryFile(OutputWindowPane output, string filename)
            {
				return ScheduleRunCommand(output, "p4win.exe", GetUserInfoString() + " \"" + filename + "\"", System.IO.Path.GetDirectoryName(filename));
            }

			private static string GetUserInfoString()
			{
				// NOTE: This to allow the user to have a P4CONFIG variable and connect to multiple perforce servers seamlessly.
				if( Singleton<Config>.Instance.useSystemEnv )
					return "";
					
				string arguments = "";
				arguments += " -p " + Singleton<Config>.Instance.port;
				arguments += " -u " + Singleton<Config>.Instance.username;
				arguments += " -c " + Singleton<Config>.Instance.client;
				arguments += " ";
				return arguments;						
			}

			public static bool TimeLapseView(OutputWindowPane output, string filename)
			{
				// NOTE: The timelapse view uses the undocumented feature for bringing up the timelapse view. The username, client and port needs to be given in a certain order to work (straight from perforce).
				string arguments = " -win 0 ";
				arguments += " -p " + Singleton<Config>.Instance.port;
				arguments += " -u " + Singleton<Config>.Instance.username;
				arguments += " -c " + Singleton<Config>.Instance.client;
				arguments += " -cmd \"annotate -i " + filename + "\"";
				return ScheduleRunCommand(output, "p4v.exe", arguments, System.IO.Path.GetDirectoryName(filename));
			}
            
            private static bool ScheduleRunCommand(OutputWindowPane output, string executableName, string command, string workingDirectory)
            {
				Command cmd = new Command();
				cmd.output = output;
				cmd.exe = executableName;
				cmd.arguments = command;
				cmd.workingDir = workingDirectory;
				cmd.sequence = m_commandCount++;
				try
				{
					m_queueLock.WaitOne();
					m_commandQueue.Enqueue(cmd);
				}
				finally
				{
					m_queueLock.ReleaseMutex();
				}
				
				m_startEvent.Release();
				output.OutputString(string.Format( "{0}: Scheduled {1} {2}\n", cmd.sequence, cmd.exe, cmd.arguments ) );
				return true;
            }
            
            public static bool RunCommand(OutputWindowPane output, string executableName, string command, string workingDirectory, int sequence)
			{
				System.Diagnostics.Process process = new System.Diagnostics.Process();
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.FileName = executableName;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WorkingDirectory = workingDirectory;
				process.StartInfo.Arguments = command;
				if (!process.Start())
				{
					if (null != output)
					{
						output.OutputString(string.Format( "{0}: Failed to start {1}. Is Perforce installed and in the path?\n", sequence, executableName ));
					}
					return false;
				}
				process.WaitForExit();

				string stdOut = process.StandardOutput.ReadToEnd();
				string stdErr = process.StandardError.ReadToEnd();

				if (null != output)
				{
					output.OutputString(sequence.ToString() + ": " + executableName + " " + command + "\n");
					output.OutputString(stdOut);
					output.OutputString(stdErr);
				}

				System.Diagnostics.Debug.WriteLine(command + "\n");
				System.Diagnostics.Debug.WriteLine(stdOut);
				System.Diagnostics.Debug.WriteLine(stdErr);

				if (0 != process.ExitCode)
				{
					if (null != output)
					{
                        output.OutputString(sequence.ToString() + ": Process exit code was " + process.ExitCode + ".\n");
					}
					return false;
				}
				return true;
			}
			
			private class Command
			{
				public string exe = "";
				public string arguments = "";
				public string workingDir = "";
				public OutputWindowPane output = null;
				public int sequence = 0;
				
				
				public void Run()
				{
					P4Operations.RunCommand(output, exe, arguments, workingDir, sequence);
				}
			};
			
			static private Mutex m_queueLock = new Mutex();
			static private Semaphore m_startEvent = new Semaphore(0, 1);
			static private Queue<Command> m_commandQueue = new Queue<Command>();
			static private System.Threading.Thread m_helperThread;
			static private int m_commandCount = 0;
			
			public static void InitThreadHelper()
			{
				m_helperThread = new System.Threading.Thread(new ThreadStart(ThreadMain));
				m_helperThread.Start();
			}

			public static void KillThreadHelper()
			{
				m_helperThread.Abort();
			}
			
			static public void ThreadMain()
			{
				while( true )
				{
					m_startEvent.WaitOne();
					Command cmd = null;
					
					try
					{
						m_queueLock.WaitOne();
						cmd = m_commandQueue.Dequeue();
					}
					finally
					{
						m_queueLock.ReleaseMutex();
					}

					try
					{
						System.Threading.Thread thread = new System.Threading.Thread( new ThreadStart( cmd.Run ) );
						thread.Start();
					}
					catch
					{
					}
				}
			}
		}
	}

}
