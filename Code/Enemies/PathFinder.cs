using System.Collections.Generic;

/// <summary>
/// Lightweight A* pathfinder that navigates the tile grid using <see cref="TreeManager"/>
/// obstacle data. All coordinates are in world space; tile conversion is internal.
/// Enemies should call <see cref="FindPath"/> at most once every ~0.4s and cache the result.
/// </summary>
public static class PathFinder
{
	/// <summary>Maximum tiles expanded before giving up. Keeps per-call CPU cost bounded.</summary>
	private const int MaxExpanded = 900;

	/// <summary>Cost of moving diagonally one tile (√2).</summary>
	private const float DiagCost = 1.414f;

	private static readonly (int dx, int dy)[] Dirs8 =
	{
		( 1,  0), (-1,  0), ( 0,  1), ( 0, -1),
		( 1,  1), (-1,  1), ( 1, -1), (-1, -1),
	};

	/// <summary>
	/// Returns a world-space waypoint list from <paramref name="from"/> toward
	/// <paramref name="to"/>, navigating around trees. Returns <c>null</c> when no path
	/// exists within the node budget or the positions are in the same tile.
	/// The list does NOT include the start tile; the last entry is the goal tile center.
	/// </summary>
	public static List<Vector3> FindPath( Vector3 from, Vector3 to )
	{
		int sx = TileX( from.x ), sy = TileY( from.y );
		int gx = TileX( to.x ),   gy = TileY( to.y );

		if ( sx == gx && sy == gy ) return null;

		// When the goal tile is a tree, walk the border outward to find the nearest open tile
		if ( TreeManager.IsTreeAtTile( gx, gy ) )
		{
			bool found = false;
			for ( int r = 1; r <= 8 && !found; r++ )
			{
				for ( int ddx = -r; ddx <= r && !found; ddx++ )
				for ( int ddy = -r; ddy <= r && !found; ddy++ )
				{
					if ( System.Math.Abs( ddx ) != r && System.Math.Abs( ddy ) != r ) continue;
					if ( !TreeManager.IsTreeAtTile( gx + ddx, gy + ddy ) )
					{ gx += ddx; gy += ddy; found = true; }
				}
			}
			if ( !found ) return null;
		}

		var gScore  = new Dictionary<(int, int), float>();
		var parent  = new Dictionary<(int, int), (int, int)>();
		var closed  = new HashSet<(int, int)>();
		// Open set: list of (f, x, y); we pop the minimum each iteration
		var open    = new List<(float f, int x, int y)>();

		gScore[(sx, sy)] = 0f;
		open.Add( (Heuristic( sx, sy, gx, gy ), sx, sy) );

		int expanded = 0;
		while ( open.Count > 0 && expanded < MaxExpanded )
		{
			// Pop node with smallest f
			int minIdx = 0;
			for ( int i = 1; i < open.Count; i++ )
				if ( open[i].f < open[minIdx].f ) minIdx = i;

			var (_, cx, cy) = open[minIdx];
			open[minIdx] = open[open.Count - 1];
			open.RemoveAt( open.Count - 1 );

			var cur = (cx, cy);
			if ( closed.Contains( cur ) ) continue;
			closed.Add( cur );
			expanded++;

			if ( cx == gx && cy == gy )
				return BuildPath( parent, sx, sy, gx, gy );

			float cg = gScore.TryGetValue( cur, out float cv ) ? cv : float.MaxValue;

			foreach ( var (ddx, ddy) in Dirs8 )
			{
				int nx = cx + ddx, ny = cy + ddy;
				if ( TreeManager.IsTreeAtTile( nx, ny ) ) continue;

				var next = (nx, ny);
				if ( closed.Contains( next ) ) continue;

				// Prevent clipping through diagonal tree corners
				bool diag = ddx != 0 && ddy != 0;
				if ( diag && (TreeManager.IsTreeAtTile( cx + ddx, cy ) || TreeManager.IsTreeAtTile( cx, cy + ddy )) )
					continue;

				float ng = cg + (diag ? DiagCost : 1f);
				if ( !gScore.TryGetValue( next, out float old ) || ng < old )
				{
					gScore[next] = ng;
					parent[next] = cur;
					open.Add( (ng + Heuristic( nx, ny, gx, gy ), nx, ny) );
				}
			}
		}

		return null;
	}

	// ── helpers ───────────────────────────────────────────────────────────

	static List<Vector3> BuildPath( Dictionary<(int, int), (int, int)> parent,
	                                int sx, int sy, int gx, int gy )
	{
		var path = new List<Vector3>();
		var cur  = (gx, gy);
		var start = (sx, sy);

		while ( cur != start )
		{
			path.Add( TileCenter( cur.Item1, cur.Item2 ) );
			cur = parent[cur];
		}

		path.Reverse();
		return path;
	}

	/// <summary>Octile heuristic — admissible for 8-directional tile movement.</summary>
	static float Heuristic( int ax, int ay, int bx, int by )
	{
		float dx = MathF.Abs( ax - bx );
		float dy = MathF.Abs( ay - by );
		return dx + dy + (DiagCost - 2f) * MathF.Min( dx, dy );
	}

	static int   TileX( float wx ) => (int)MathF.Floor( wx / TreeManager.TileWorldWidth );
	static int   TileY( float wy ) => (int)MathF.Floor( wy / TreeManager.TileWorldHeight );

	static Vector3 TileCenter( int tx, int ty ) => new(
		tx * TreeManager.TileWorldWidth  + TreeManager.TileWorldWidth  * 0.5f,
		ty * TreeManager.TileWorldHeight + TreeManager.TileWorldHeight * 0.5f,
		0f
	);
}
