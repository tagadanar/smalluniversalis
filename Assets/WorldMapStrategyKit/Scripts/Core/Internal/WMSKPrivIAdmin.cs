// World Map Strategy Kit for Unity - Main Script
// (C) 2016-2021 by Ramiro Oliva (Kronnect)
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WMSK

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using WorldMapStrategyKit.ClipperLib;

namespace WorldMapStrategyKit {

	public partial class WMSK : MonoBehaviour {

		#region Common IAdmin functionality


		public void MergeAdjacentRegions (IAdminEntity entity) {
			// Searches for adjacency - merges in first region
			int regionCount = entity.regions.Count;
			for (int k = 0; k < regionCount; k++) {
				Region region1 = entity.regions [k];
				if (region1 == null || region1.points == null || region1.points.Length == 0)
					continue;
				for (int j = k + 1; j < regionCount; j++) {
					Region region2 = entity.regions [j];
					if (region2 == null || region2.points == null || region2.points.Length == 0)
						continue;
					if (!region1.Intersects (region2))
						continue;
					RegionMagnet (region1, region2);
					Clipper clipper = new Clipper ();
					clipper.AddPath (region1, PolyType.ptSubject);
					clipper.AddPath (region2, PolyType.ptClip);
					clipper.Execute (ClipType.ctUnion);

					// Add new neighbours
					int rnCount = region2.neighbours.Count;
					for (int n = 0; n < rnCount; n++) {
						Region neighbour = region2.neighbours [n];
						if (neighbour != null && neighbour != region1 && !region1.neighbours.Contains (neighbour)) {
							region1.neighbours.Add (neighbour);
						}
					}
					// Remove merged region

					entity.regions.RemoveAt (j);
					region1.sanitized = false;
					j = k;
					regionCount--;
					entity.mainRegionIndex = 0;	// will need to refresh country definition later in the process
				}
			}
		}

		#endregion

	}

}