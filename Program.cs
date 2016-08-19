using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FileMeddler
{
	class Program
	{
		private const int kRetryMilliseconds = 50;
		private const int kLockMilliseconds = 200;
		private const int kGiveUpMilliseconds = 5000;

		private static readonly HashSet<string> s_extensionsToIgnore = new HashSet<string> { ".exe", ".dll", ".ini", ".pdb" };

		//		private static ConcurrentDictionary<string,int> _lockedFiles = new ConcurrentDictionary<string, byte>();
		private static readonly HashSet<string> s_filesInProcess = new HashSet<string>();
		private static string s_root = "";

		private static void Main(string[] args)
		{
			var consoleColor = Console.ForegroundColor;

			var filter = "*.*";
			if (args.Length == 1)
				filter = args[0];

			s_root = Directory.GetCurrentDirectory();

			var watcher = new FileSystemWatcher()
			{
				Path = s_root,
				Filter = filter
			};
			watcher.Created += SomethingHappened;
			watcher.Changed += SomethingHappened;
			watcher.Renamed += SomethingHappened;
			watcher.Deleted += SomethingHappened;
			watcher.IncludeSubdirectories = true;

			watcher.EnableRaisingEvents = true;

			Console.WriteLine("Ready to meddle. Press Enter to stop.");
			Console.ReadLine();
			Console.ForegroundColor = consoleColor; // return text to the original color
		}


		private static void SomethingHappened(object sender, FileSystemEventArgs e)
		{
			//the FileWatcher won't give us a new one until
			//we return. Since timing is the whole point here,
			//we spawn a thread to try and grab that file and return quickly.
			var t = new Thread(() => CampOnFile(e));
			t.Start();
		}

		private static void CampOnFile(FileSystemEventArgs e)
		{
			if(Directory.Exists(e.FullPath))
				return; //it's not a file

			var filename = Path.GetFileName(e.FullPath);

			var extension = Path.GetExtension(filename);
			if (s_extensionsToIgnore.Contains(extension.ToLowerInvariant()))
				return;

			if (s_filesInProcess.Contains(e.FullPath))
			{
				//Print(ConsoleColor.Gray, "   Already processing: " + filename);
				return;
			}
			else
			{
				s_filesInProcess.Add(e.FullPath);
			}

			var startTime = DateTime.Now.AddMilliseconds(kGiveUpMilliseconds);
			var reportedWaiting = false;
			var relativePath = e.FullPath.Replace(s_root, "") + " ";
			switch (e.ChangeType)
			{
				case WatcherChangeTypes.Created:
					Print(ConsoleColor.DarkMagenta, "Creation: " + relativePath);
					break;
				case WatcherChangeTypes.Deleted:
					Print(ConsoleColor.DarkRed, "Deletion: " + relativePath);
					s_filesInProcess.Remove(e.FullPath);
					return;
				case WatcherChangeTypes.Changed:
					Print(ConsoleColor.Cyan, "Modified: " + relativePath);
					break;
				case WatcherChangeTypes.Renamed:
					Print(ConsoleColor.DarkCyan, "Renamed: " + relativePath);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			do
			{
				try
				{
					using(File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
					{
						
						Print(ConsoleColor.Yellow, "   Locking: " + filename);
						Thread.Sleep(kLockMilliseconds);
					}
					Print(ConsoleColor.Green, "   Released: " + filename);
					s_filesInProcess.Remove(e.FullPath);
					return;
				}
				catch(FileNotFoundException)
				{
					Print(ConsoleColor.DarkGreen, "   File gone: " + filename);
					s_filesInProcess.Remove(e.FullPath);
					return;
				}
				catch(Exception error)
				{
					if(DateTime.Now > startTime)
					{
						Print(ConsoleColor.Red, "   Giving up waiting for: " + filename);
						Print(ConsoleColor.Red, error.Message);
						s_filesInProcess.Remove(e.FullPath);
						return;
					}
					if(!reportedWaiting)
					{
						Print(ConsoleColor.Magenta, "   Waiting to acquire: " + filename);
					}
					reportedWaiting = true;
					Thread.Sleep(kRetryMilliseconds);
				}
			} while(true);
		}

		private static void Print(ConsoleColor color, string message)
		{
			lock(Console.Out)
			{
				Console.ForegroundColor = color;
				Console.WriteLine(message);
			}
		}
	}
}
