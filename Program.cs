using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileMeddler
{
	class Program
	{
		private static int kRetryMilliseconds = 50;
		private static int kLockMilliseconds = 2000;
		private static int kGiveUpMilliseconds = 5000;

		//		private static ConcurrentDictionary<string,int> _lockedFiles = new ConcurrentDictionary<string, byte>();
		private static HashSet<string> _filesInProcess = new HashSet<string>();
		private static string _root = "";
		static void Main(string[] args)
		{
			_root = Directory.GetCurrentDirectory();

			var watcher = new FileSystemWatcher()
			{
				Path = _root,
				Filter = "*.*"

			};
			watcher.Created += SomethingHappened;
			watcher.Changed += SomethingHappened;
			watcher.Renamed += SomethingHappened;
			watcher.Deleted += SomethingHappened;
			watcher.IncludeSubdirectories = true;

			watcher.EnableRaisingEvents = true;

			Console.WriteLine("Ready to meddle. Press Enter to stop.");
			Console.ReadLine();
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

			if (_filesInProcess.Contains(e.FullPath))
			{
				//Print(ConsoleColor.Gray, "   Already processing: " + filename);
				return;
			}
			else
			{
				_filesInProcess.Add(e.FullPath);
			}

			var startTime = DateTime.Now.AddMilliseconds(kGiveUpMilliseconds);
			var reportedWaiting = false;
			var relativePath = e.FullPath.Replace(_root, "") + " ";
			switch (e.ChangeType)
			{
				case WatcherChangeTypes.Created:
					Print(ConsoleColor.DarkMagenta, "Creation: " + relativePath);
					break;
				case WatcherChangeTypes.Deleted:
					Print(ConsoleColor.DarkRed, "Deletion: " + relativePath);
					_filesInProcess.Remove(e.FullPath);
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
					Print(ConsoleColor.Green, "   Released: filename" + filename);
					_filesInProcess.Remove(e.FullPath);
					return;
				}
				catch(FileNotFoundException)
				{
					Print(ConsoleColor.DarkGreen, "   File gone: " + filename);
					_filesInProcess.Remove(e.FullPath);
					return;
				}
				catch(Exception error)
				{
					if(DateTime.Now > startTime)
					{
						Print(ConsoleColor.Red, "   Giving up waiting for: " + filename);
						Print(ConsoleColor.Red, error.Message);
						_filesInProcess.Remove(e.FullPath);
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
