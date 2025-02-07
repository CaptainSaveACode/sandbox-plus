using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Sandbox.UI.Tests;
using System.Text.RegularExpressions;
using System;
using Sandbox.UI.Construct;

namespace Sandbox.UI
{
	[Library]
	public partial class ModelSelector : Panel
	{
		private static Dictionary<string, HashSet<string>> SpawnLists = new();
		private static bool spawnListsLoaded = false;
		VirtualScrollPanel Canvas;

		private static readonly Regex reModelMatGroup = new( @"^(.*?)(?:--(\d+))?\.vmdl$" );
		private static readonly Regex reSpawnlistFile = new( @"([^\.]+)\.spawnlist$" );
		public ModelSelector( IEnumerable<string> spawnListNames, bool showMaterialGroups = false )
		{
			AddClass( "modelselector" );
			AddChild( out Canvas, "canvas" );

			Canvas.Layout.AutoColumns = true;
			Canvas.Layout.ItemWidth = 64;
			Canvas.Layout.ItemHeight = 64;
			Canvas.OnCreateCell = ( cell, data ) =>
			{
				var file = (string)data;
				Panel panel;

				if ( FileSystem.Mounted.FileExists( file + "_c.png" ) )
				{
					panel = cell.Add.Panel( "icon" );
					panel.Style.BackgroundImage = Texture.Load( $"/{file}_c.png", false );
				}
				else
				{
					// Cloud models don't have vmdl_c.png spawnicons, so we have to render them ourselves
					var sceneWorld = new SceneWorld();
					var sceneLight = new SceneLight( sceneWorld, Vector3.Up * 75.0f, 200.0f, Color.White * 20.0f );

					// todo: how does the Model devtool calculate a nice angle for its autogenerated spawnicons?
					var sceneModel = new SceneModel( sceneWorld, file, new Transform( Vector3.Zero, new Rotation( new Vector3( -0.25f, 0.25f, 0.9f ), 0f ) ) );

					panel = cell.Add.ScenePanel( sceneWorld, Vector3.Up * (sceneModel.Model.Bounds.Size.Length + 10), new Angles( 90, 0, 0 ).ToRotation(), 45, "icon" );
					(panel as ScenePanel).RenderOnce = true;
				}

				var match = reModelMatGroup.Match( file );
				panel.AddEventListener( "onclick", () =>
				{
					var currentTool = ConsoleSystem.GetValue( "tool_current" );
					ConsoleSystem.Run( $"{currentTool}_model", match.Groups[1] + ".vmdl" );
					ConsoleSystem.Run( $"{currentTool}_materialgroup", match.Groups.Count > 2 ? match.Groups[2] : 0 );
				} );
			};

			var spawnList = spawnListNames.SelectMany( GetSpawnList );

			foreach ( var file in spawnList )
			{
				Canvas.AddItem( file );
			}
			// VirtualScrollPanel doesn't have a valid height (subsequent children overlap it within flex-direction: column) so calculate it manually
			Style.Height = (64 + 6) * (int)Math.Ceiling( spawnList.Count() / 5f );
		}

		/// To add models/materials to the spawnlists:
		/// either call these functions in your addon init, like `ModelSelector.AddToSpawnlist( "thruster", new string[] {"models/blah.vmdl"} )`
		/// or add an `addonname.thruster.spawnlist` file (newline delimited list of models)
		public static void AddToSpawnlist( string list, string model )
		{
			SpawnLists.GetOrCreate( list ).Add( model );
		}
		public static void AddToSpawnlist( string list, IEnumerable<string> models )
		{
			SpawnLists.GetOrCreate( list ).UnionWith( models );
		}

		public static IEnumerable<string> GetSpawnList( string list )
		{
			if ( !spawnListsLoaded )
			{
				InitializeSpawnlists();
			}
			return SpawnLists.GetOrCreate( list );
		}

		[ConCmd.Client( "reload_spawnlists" )]
		public static void InitializeSpawnlists()
		{
			SpawnLists.Clear();
			spawnListsLoaded = true;
			foreach ( var file in FileSystem.Mounted.FindFile( "/", "*.spawnlist", true ) )
			{
				var match = reSpawnlistFile.Match( file );
				var listName = match.Groups[1].Value;
				var models = FileSystem.Mounted.ReadAllText( file ).Trim().Split( '\n' ).Select( x => x.Trim() );
				AddToSpawnlist( listName, models );
			}
			Event.Run( "spawnlists.initialize" );
		}
	}
}
