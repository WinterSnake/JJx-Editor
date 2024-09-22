/*
	Junk Jack X: Core
	- World

	Segment Breakdown:
	---------------------------------------------------------------------------------------------------------------------
	:<Info>
	Segment[0xF0  :   0xFF] = UUID                 | Length:  16 (0x10) | Type: uuid
	Segment[0x100 :  0x103] = Last Played Datetime | Length:   4  (0x4) | Type: uint             | Parent: DateTime
	Segment[0x104 :  0x107] = Game Version         | Length:   4  (0x4) | Type: enum[uint32]     | Parent: JJx.Version
	Segment[0x108 :  0x127] = Name                 | Length:  32 (0x20) | Type: char*
	Segment[0x128 :  0x137] = Author               | Length:  16 (0x10) | Type: char*
	Segment[0x138 :  0x139] = World.Width          | Length:   2  (0x2) | Type: uint16
	Segment[0x13A :  0x13B] = World.Height         | Length:   2  (0x2) | Type: uint16t
	Segment[0x13C :  0x13D] = Player.X             | Length:   2  (0x2) | Type: uint16
	Segment[0x13E :  0x13F] = Player.Y             | Length:   2  (0x2) | Type: uint16
	Segment[0x140 :  0x141] = Spawn.X              | Length:   2  (0x2) | Type: uint16
	Segment[0x142 :  0x143] = Spawn.Y              | Length:   2  (0x2) | Type: uint16
	Segment[0x144 :  0x147] = Planet               | Length:   4  (0x4) | Type: enum[uint32]     | Parent: Planet
	Segment[0x148]          = Season               | Length:   1  (0x1) | Type: enum[uint8]      | Parent: Season
	Segment[0x149]          = Gamemode             | Length:   1  (0x1) | Type: enum[uint8]      | Parent: Gamemode
	Segment[0x14A]          = World Size           | Length:   1  (0x1) | Type: enum[uint8]      | Parent: InitSize
	Segment[0x14B]          = Sky Size             | Length:   1  (0x1) | Type: enum[uint8]      | Parent: InitSize
	Segment[0x14C :  0x14F] = UNKNOWN              | Length:   4  (0x4) | Type: ???
	Segment[0x150 :  0x1CF] = Padding              | Length: 128 (0x80) | Type: uint32[32] = {0}
	---------------------------------------------------------------------------------------------------------------------

	Written By: Ryan Smith
*/
using System;
using System.Diagnostics;
using System.IO;
using JJx.Serialization;

namespace JJx;

public enum Gamemode : byte
{
	Survival  = 0x0,
	Creative  = 0x1,
	Flat      = 0x2,
	Adventure = 0x3,
}

public sealed class World
{
	/* Constructors */
	internal World(
		Guid uid, DateTime lastPlayed, Version version, string name, string author, (ushort, ushort) player, (ushort, ushort) spawn,
		Planet planet, Season season, Gamemode gamemode, SizeType sizeType, SizeType skySizeType, ushort[] skyline, TileMap tileMap
	)
	{
		// Info
		this.Uid = uid;
		this.LastPlayed = lastPlayed;
		this.Version = version;
		this._Name = name;
		this._Author = author;
		this.Planet = planet;
		this.Season = season;
		this.Gamemode = gamemode;
		this.SizeType = sizeType;
		this.SkySizeType = skySizeType;
		// Tiles
		this.Skyline = skyline;
		this._TileMap = tileMap;
	}
	/* Instance Methods */
	public void Save(string filePath)
	{
		using var stream = ArchiverStream.Writer(filePath, ArchiverStreamType.World);
		this.ToStream(stream);
	}
	public void ToStream(ArchiverStream stream)
	{
		if (stream.Type != ArchiverStreamType.World)
			throw new ArgumentException($"Passed in a non-world ({stream.Type}) archiver stream to World.ToStream()");
		if (!stream.CanWrite)
			throw new ArgumentException("Passed in a non-writable archiver stream to World.ToStream()");
	}
	/* Static Methods */
	public static World Load(string filePath)
	{
		using var stream = ArchiverStream.Reader(filePath);
		return World.FromStream(stream);
	}
	public static unsafe World FromStream(ArchiverStream stream)
	{
		if (stream.Type != ArchiverStreamType.World)
			throw new ArgumentException($"Passed in a non-world ({stream.Type}) archiver stream to World.FromStream()");
		if (!stream.CanRead)
			throw new ArgumentException("Passed in a non-readable archiver stream to World.FromStream()");
		ArchiverChunk chunk;
		var reader = new JJxReader(stream);
		/// Info
		chunk = stream.GetChunk(ArchiverChunkType.WorldInfo);
		Debug.Assert(stream.Position == chunk.Position, $"ArchiverStream::Reader not aligned with WorldInfoChunk || Current: {stream.Position:X8} ; Expected: {chunk.Position:X8}");
		if (stream.Position != chunk.Position)
			stream.Position  = chunk.Position;
		var uid = reader.Get<Guid>();
		var lastPlayed = reader.Get<DateTime>();
		var version = reader.Get<Version>();
		var name = reader.GetString(length: SIZEOF_NAME);
		var author = reader.GetString(length: SIZEOF_AUTHOR);
		(ushort width, ushort height) size = (reader.GetUInt16(), reader.GetUInt16());
		(ushort, ushort) player = (reader.GetUInt16(), reader.GetUInt16());
		(ushort, ushort) spawn  = (reader.GetUInt16(), reader.GetUInt16());
		var planet = reader.Get<Planet>();
		var season = reader.Get<Season>();
		var gamemode = reader.Get<Gamemode>();
		var sizeType = reader.Get<SizeType>();
		var skySizeType = reader.Get<SizeType>();
		// -UNKNOWN(4)- \\
		reader.GetBytes(4);
		// Padding
		reader.Skip(SIZEOF_PADDING);
		/// Skyline
		chunk = stream.GetChunk(ArchiverChunkType.WorldSkyline);
		Debug.Assert(stream.Position == chunk.Position, $"ArchiverStream::Reader not aligned with WorldSkylineChunk || Current: {stream.Position:X8} ; Expected: {chunk.Position:X8}");
		if (stream.Position != chunk.Position)
			stream.Position  = chunk.Position;
		var skyline = new ushort[size.width];
		for (var i = 0; i < skyline.Length; ++i)
			skyline[i] = reader.GetUInt16();
		/// Blocks
		chunk = stream.GetChunk(ArchiverChunkType.WorldBlocks);
		Debug.Assert(stream.Position == chunk.Position, $"ArchiverStream::Reader not aligned with WorldBlocksChunk || Current: {stream.Position:X8} ; Expected: {chunk.Position:X8}");
		if (stream.Position != chunk.Position)
			stream.Position  = chunk.Position;
		// TODO: Figure out why GZipStream reads more than asked through this single line method (try to remove memorystream requirement)
		// var tileMap = reader.Get<TileMap>(chunk.Compressed, size.width, size.height);
		TileMap tileMap;
		var tileMapBytes = reader.GetBytes((int)chunk.Length);
		fixed (byte* tileMapPin = tileMapBytes)
		{
			var tileMapStream = new UnmanagedMemoryStream(tileMapPin, tileMapBytes.Length);
			var tileMapReader = new JJxReader(tileMapStream);
			tileMap = tileMapReader.Get<TileMap>(chunk.Compressed, size.width, size.height);
		}
		/// Layer: Fog
		var fogChunk = stream._GetChunk(ArchiverChunkType.WorldFog);
		if (fogChunk != null)
		{
			chunk = fogChunk.Value;
			Debug.Assert(stream.Position == chunk.Position, $"ArchiverStream::Reader not aligned with WorldFogChunk || Current: {stream.Position:X8} ; Expected: {chunk.Position:X8}");
			if (stream.Position != chunk.Position)
				stream.Position  = chunk.Position;
		}
		/// Time
		chunk = stream.GetChunk(ArchiverChunkType.WorldTime);
		Debug.Assert(stream.Position == chunk.Position, $"ArchiverStream::Reader not aligned with WorldTimeChunk || Current: {stream.Position:X8} ; Expected: {chunk.Position:X8}");
		if (stream.Position != chunk.Position)
			stream.Position  = chunk.Position;
		/// World
		return new World(
			// Info
			uid, lastPlayed, version, name, author, player, spawn, planet, season, gamemode, sizeType, skySizeType,
			// Tiles
			skyline, tileMap
		);
	}
	/* Properties */
	// Info
	public readonly Guid Uid = Guid.NewGuid();
	public DateTime LastPlayed = DateTime.Now;
	public readonly Version Version = Version.Latest;
	private string _Name;
	public string Name {
		get { return this._Name; }
		set {
			if (String.IsNullOrEmpty(value)) throw new ArgumentException("Name cannot be null or 0 characters long");
			else if (value.Length < SIZEOF_NAME) this._Name = value;
			else this._Name = value.Substring(0, SIZEOF_NAME - 1);
		}
	}
	private string _Author;
	public string Author {
		get { return this._Author; }
		set {
			if (String.IsNullOrEmpty(value)) throw new ArgumentException("Author cannot be null or 0 characters long");
			else if (value.Length < SIZEOF_AUTHOR) this._Author = value;
			else this._Author = value.Substring(0, SIZEOF_AUTHOR - 1);
		}
	}
	public (ushort Width, ushort Height) Size => ((ushort)this._TileMap.Tiles.GetLength(0), (ushort)this._TileMap.Tiles.GetLength(1));
	public (ushort X, ushort Y) Player;
	public (ushort X, ushort Y) Spawn;
	public Planet Planet;
	public Season Season;
	public Gamemode Gamemode;
	public SizeType SizeType;
	public SizeType SkySizeType;
	// Tiles
	public readonly ushort[] Skyline;
	internal readonly TileMap _TileMap;
	public Tile[,] Blocks => this._TileMap.Tiles;
	// Astmosphere
	// -Time
	public uint Ticks = 0;
	public Period Time = Period.Day;
	// -Weather
	public float PoissonSum = 0.0f;
	public Weather Weather = Weather.None;
	public byte PoissonSkipped = 0;
	// Layers
	// Containers
	/* Class Properties */
	private const byte SIZEOF_NAME    = 32;
	private const byte SIZEOF_AUTHOR  = 16;
	private const byte SIZEOF_PADDING = sizeof(uint) * 32;
}
