using AssetManagementBase;
using System;

namespace ThirdPersonTutorial;

class Program
{
	static void Main(string[] args)
	{
		// Configure asset management system to log to console
		AMBConfiguration.Logger = Console.WriteLine;

		// Create and run the game instance
		using (var game = new MyGame())
		{
			game.Run();
		}
	}
}
