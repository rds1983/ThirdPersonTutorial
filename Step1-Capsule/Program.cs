using AssetManagementBase;
using System;

namespace ThirdPersonTutorial;

class Program
{
	static void Main(string[] args)
	{
		AMBConfiguration.Logger = Console.WriteLine;
		using (var game = new ViewerGame())
		{
			game.Run();
		}
	}
}
