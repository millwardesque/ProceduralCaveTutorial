using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class MapGenerator : MonoBehaviour {
    public int width = 60;
    public int height = 80;
    public string seed;
    public bool useRandomSeed = true;
    public int smoothingIterations = 5;
    public int borderSize = 5;

    [Range (0, 100)]
    public int randomFillPercent;

    int[,] map;


    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GenerateMap();
        }
    }

    void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < smoothingIterations; ++i)
        {
            SmoothMap();
        }

        ProcessMap ();
       
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];
        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize) {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else {
                    borderedMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1f);
    }

    void ProcessMap() {
        List<List<Coord>> wallRegions = GetRegions (1);
        int wallThresholdSize = 50;
        foreach (List<Coord> wallRegion in wallRegions) {
            // Remove regions that are too small 
            if (wallRegion.Count < wallThresholdSize) {
                foreach (Coord tile in wallRegion) {
                    map [tile.x, tile.y] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions (0);
        int roomThresholdSize = 50;
        List<Room> survivingRooms = new List<Room> ();
        foreach (List<Coord> roomRegion in roomRegions) {
            // Remove regions that are too small 
            if (roomRegion.Count < roomThresholdSize) {
                foreach (Coord tile in roomRegion) {
                    map [tile.x, tile.y] = 1;
                }
            }
            else {
                survivingRooms.Add (new Room (roomRegion, map));
            }
        }

        survivingRooms.Sort ();
        survivingRooms [0].isMainRoom = true;
        survivingRooms [0].isAccessibleFromMainRoom = true;

        ConnectClosestRooms (survivingRooms, false);
    }

    void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom) {
        List<Room> roomListA = new List<Room> ();
        List<Room> roomListB = new List<Room> ();

        if (forceAccessibilityFromMainRoom) {
            foreach (Room room in allRooms) {
                if (room.isAccessibleFromMainRoom) {
                    roomListB.Add (room);
                }
                else {
                    roomListA.Add (room);
                }
            }
        }
        else {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int bestDistance = 0;
        Coord bestTileA = new Coord ();
        Coord bestTileB = new Coord ();
        Room bestRoomA = new Room ();
        Room bestRoomB = new Room ();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA) {
            if (!forceAccessibilityFromMainRoom) {
                possibleConnectionFound = false;

                if (roomA.connectedRooms.Count > 0) {
                    continue;
                }
            }

            foreach (Room roomB in roomListB) {
                if (roomA == roomB || roomA.IsConnected (roomB)) {
                    continue;
                }

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
                        Coord tileA = roomA.edgeTiles [tileIndexA];
                        Coord tileB = roomB.edgeTiles [tileIndexB];
                        int distance = (int)(Mathf.Pow (tileA.x - tileB.x, 2f) + Mathf.Pow (tileA.y - tileB.y, 2f));

                        if (distance < bestDistance || !possibleConnectionFound) {
                            bestDistance = distance;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }   
                }
            }

            if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosestRooms (allRooms, true);
        }

        if (!forceAccessibilityFromMainRoom) {
            ConnectClosestRooms (allRooms, true);
        }
    }

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
        Room.ConnectRooms (roomA, roomB);
        Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 100);

        List<Coord> line = GetLine (tileA, tileB);
        foreach (Coord c in line) {
            DrawCircle (c, 1);
        }
    }

    void DrawCircle(Coord c, int radius) {
        for (int x = -radius; x <= radius; ++x) {
            for (int y = -radius; y <= radius; ++y) {
                if (x * x + y * y < radius * radius) {
                    int drawX = c.x + x;
                    int drawY = c.y + y;

                    if (IsInMapRange (drawX, drawY)) {
                        map [drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    List<Coord> GetLine(Coord from, Coord to) {
        List<Coord> line = new List<Coord> ();
        int x = from.x;
        int y = from.y;
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        bool inverted = false;
        int step = Math.Sign (dx);
        int gradientStep = Math.Sign (dy);
        int longest = Mathf.Abs (dx);
        int shortest = Mathf.Abs (dy);

        if (longest < shortest) {
            inverted = true;
            longest = Mathf.Abs (dy);
            shortest = Mathf.Abs (dx);

            step = Math.Sign (dy);
            gradientStep = Math.Sign (dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; ++i) {
            line.Add (new Coord(x, y));

            if (inverted) {
                y += step;
            } else {
                x += step;
            }

            gradientAccumulation += shortest;

            while (gradientAccumulation >= longest) {
                if (inverted) {
                    x += gradientStep;
                }
                else {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 CoordToWorldPoint(Coord tile) {
        return new Vector3 (-width / 2f + 0.5f + tile.x, 2f, -height / 2f + 0.5f + tile.y);
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = DateTime.Now.ToString();
        }

        System.Random pseudoRandom = new System.Random(seed.GetHashCode());
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        int[,] newMap = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);
                if (neighbourWallTiles > 4)
                {
                    newMap[x, y] = 1;
                }
                else if (neighbourWallTiles < 4)
                {
                    newMap[x, y] = 0;
                }
                else
                {
                    newMap[x, y] = map[x, y];
                }
            }
        }

        map = newMap;
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++;
                }                
            }
        }
        return wallCount;
    }

    List<List<Coord>> GetRegions(int tileType) {
        List<List<Coord>> regions = new List<List<Coord>> ();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType) {
                    List<Coord> newRegion = GetRegionTiles (x, y);
                    regions.Add (newRegion);

                    foreach (Coord tile in newRegion) {
                        mapFlags [tile.x, tile.y] = 1;
                    }
                }
            }
        }
        return regions;
    }

    List<Coord> GetRegionTiles(int startX, int startY) {
        List<Coord> tiles = new List<Coord> ();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord> ();
        queue.Enqueue (new Coord(startX, startY));
        mapFlags [startX, startY] = 1;
        while(queue.Count > 0) {
            Coord tile = queue.Dequeue ();
            tiles.Add (tile);
            for (int x = tile.x - 1; x <= tile.x + 1; x++) {
                for (int y = tile.y - 1; y <= tile.y + 1; y++) {
                    if (IsInMapRange (x, y) && (x == tile.x || y == tile.y)) {
                        if (mapFlags[x, y] == 0 && map[x, y] == tileType) {
                            mapFlags[x, y] = 1;
                            queue.Enqueue (new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    bool IsInMapRange(int x, int y) {
        return (x >= 0 && x < width && y >= 0 && y < height);
    }

    struct Coord {
        public int x;
        public int y;

        public Coord(int x, int y) { 
            this.x = x;
            this.y = y;
        }
    }

    class Room : IComparable<Room> {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room() {

        }

        public Room(List<Coord> tiles, int[,] map) {
            this.tiles = tiles;
            roomSize = this.tiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();

            foreach (Coord tile in this.tiles) {
                for (int x = tile.x - 1; x < tile.x + 1; x++) {
                    for (int y = tile.y - 1; y < tile.y + 1; y++) {
                        if (x == tile.x || y == tile.y) {
                            if (map[x, y] == 1) {
                                edgeTiles.Add (tile);
                            }
                        }
                    }
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB) {
            if (roomA.isAccessibleFromMainRoom) {
                roomB.SetAccessibleFromMainRoom ();
            }
            else if (roomB.isAccessibleFromMainRoom) {
                roomA.SetAccessibleFromMainRoom ();
            }

            roomA.connectedRooms.Add (roomB);
            roomB.connectedRooms.Add (roomA);
        }

        public bool IsConnected(Room room) {
            return connectedRooms.Contains (room);
        }

        public int CompareTo(Room otherRoom) {
            return otherRoom.roomSize.CompareTo (roomSize);
        }

        public void SetAccessibleFromMainRoom() {
            if (!isAccessibleFromMainRoom) {
                isAccessibleFromMainRoom = true;

                foreach (Room room in connectedRooms) {
                    room.SetAccessibleFromMainRoom ();
                }
            }
        }
    }
}
