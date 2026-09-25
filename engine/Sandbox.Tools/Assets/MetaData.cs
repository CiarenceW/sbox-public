using System.Text.Json;
using System.Text.Json.Nodes;

namespace Editor
{
	/// <summary>
	/// A class to CRUD json files. This should probably be a generic class since it seems
	/// like we might want to do this with stuff other than meta files. But there's no need for
	/// that right now, so lets leave it simple.
	/// </summary>
	public class MetaData
	{
		/// <summary>
		/// File path to the metadata file.
		/// </summary>
		public string FilePath { get; }

		internal MetaData( string sourceFile )
		{
			FilePath = sourceFile;
		}

		/// <summary>
		/// Note - not caching anything here, and reading the whole json file
		/// every time. Lets see how this turns out.
		/// </summary>
		JsonElement? Read()
		{
			var recasedPath = CaseInsensitivePhysicalFileSystem.ResolveNativeCasing( FilePath );

			if ( !System.IO.File.Exists( recasedPath ) )
				return null;

			try
			{
				var json = System.IO.File.ReadAllText( recasedPath );

				var document = JsonDocument.Parse( json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip } );
				return document.RootElement;
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"Couldn't parse '{recasedPath}' ({e.Message})" );
				return null;
			}
		}

		/// <summary>
		/// Note - not caching anything here, and reading the whole json file
		/// every time. Lets see how this turns out.
		/// </summary>
		JsonObject StartWrite()
		{
			var o = new JsonNodeOptions { PropertyNameCaseInsensitive = true };

			var e = Read();
			if ( e is null || e.Value.ValueKind == JsonValueKind.Null )
				return new JsonObject( o );

			return JsonObject.Create( e.Value, o );
		}

		void Save( JsonObject obj )
		{
			const int retries = 10;

			var recasedPath = CaseInsensitivePhysicalFileSystem.ResolveNativeCasing ( FilePath );

			for ( var i = 0; i < retries; i++ )
			{
				try
				{
					using ( var stream = System.IO.File.Open( recasedPath, System.IO.FileMode.Create ) )
					{
						using ( Utf8JsonWriter writer = new Utf8JsonWriter( stream, new JsonWriterOptions { Indented = true, SkipValidation = true } ) )
						{
							obj.WriteTo( writer );
						}
					}

					return;
				}
				catch ( System.IO.IOException ex )
				{
					const int delay = 100;
					Log.Warning( $"Failed to save {recasedPath} ({ex.Message}). Retrying in {delay}ms... ({i + 1}/{retries})" );
					System.Threading.Thread.Sleep( delay );
				}
			}
		}

		public JsonElement? GetElement( string keyName )
		{
			var root = Read();

			if ( root == null )
				return null;

			if ( root.Value.ValueKind != JsonValueKind.Object )
				return null;

			if ( !root.Value.TryGetProperty( keyName, out var value ) )
				return null;

			return value;
		}

		public T Get<T>( string keyName, T defaultValue = default( T ) )
		{
			try
			{
				// Handle errors from Read() as well but only for reading.
				// If we do it for StartSave, we risk losing data..
				var e = GetElement( keyName );
				if ( e == null ) return defaultValue;

				return e.Value.Deserialize<T>( JsonSerializerOptions.Default ) ?? defaultValue;
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"MetaData.Get<{typeof( T )}>( '{keyName}' ) - {e.Message}" );

				// if it was the wrong type, we don't care
				return defaultValue;
			}
		}

		public bool TryGet<T>( string keyName, out T value )
		{
			try
			{
				// Handle errors from Read() as well but only for reading.
				// If we do it for StartSave, we risk losing data..
				if ( GetElement( keyName ) is { } e && e.Deserialize<T>( JsonSerializerOptions.Default ) is T result )
				{
					value = result;
					return true;
				}
			}
			catch
			{
				// if it was the wrong type, we don't care
			}

			value = default;
			return false;
		}

		public string GetString( string keyName, string defaultValue = default ) => Get<string>( keyName, defaultValue );
		public bool GetBool( string keyName, bool defaultValue = default ) => Get<bool>( keyName, defaultValue );
		public int GetInt( string keyName, int defaultValue = default ) => Get<int>( keyName, defaultValue );
		public float GetFloat( string keyName, float defaultValue = default ) => Get<float>( keyName, defaultValue );

		/// <summary>
		/// Set a value in the metadata file. If the value is null, the key will be removed.
		/// </summary>
		public void Set<T>( string name, T value )
		{
			var writer = StartWrite();

			if ( writer.ContainsKey( name ) )
				writer.Remove( name );

			if ( value != null )
			{
				var valueNode = JsonSerializer.SerializeToNode( value );
				writer.Add( name, valueNode );
			}

			Save( writer );
		}

		// //nuclear option, full recase, lol!
		// static string RecasePath( string path )
		// {
		// 	string[] splitPath = path.Split( '/', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries );

		// 	//re-root
		// 	splitPath[0] = '/' + splitPath[0];

		// 	string recasedPath = splitPath[0];

		// 	//shouldn't happen
		// 	bool isNextEntryAFile = splitPath.Length == 2;

		// 	//start after the root path
		// 	for ( int splitPath_Index = 1; splitPath_Index < splitPath.Length; splitPath_Index++ )
		// 	{
		// 		if ( isNextEntryAFile )
		// 		{
		// 			foreach ( var dirFiles in Directory.GetFiles( recasedPath ) )
		// 			{
		// 				var combinedPath = Path.Combine(  );
		// 			}
		// 		}
		// 		else
		// 		{
		// 			foreach ( var dirSubDirectories in Directory.GetDirectories( recasedPath ) )
		// 			{
		// 				var combinedPath = Path.Combine( recasedPath, splitPath[splitPath_Index] );

		// 				if ( string.Compare( combinedPath, dirSubDirectories, ignoreCase: true ) == 0 )
		// 				{
		// 					recasedPath = combinedPath;
		// 				}
		// 			}
		// 		}

		// 		isNextEntryAFile = splitPath_Index + 1 == splitPath_Index - 1;
		// 	}

		// 	return null;
		// }
	}
}
