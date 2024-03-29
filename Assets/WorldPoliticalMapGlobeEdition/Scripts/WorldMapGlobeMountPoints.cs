// World Political Map - Globe Edition for Unity - Main Script
// Created by Ramiro Oliva (Kronnect)
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WPM
// ***************************************************************************
// This is the public API file - every property or public method belongs here
// ***************************************************************************

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace WPM {

    /* Public WPM Class */
    public partial class WorldMapGlobe : MonoBehaviour {

        /// <summary>
        /// Complete list of mount points.
        /// </summary>
        [NonSerialized]
        public List<MountPoint> mountPoints;
        const string MOUNTPOINTS_ATTRIB_DEFAULT_FILENAME = "mountPoints";

        #region Public API area

        [SerializeField]
        string _mountPointsAttributeFile = MOUNTPOINTS_ATTRIB_DEFAULT_FILENAME;

        /// <summary>
        /// Returns the file name corresponding to the current mount point data file
        /// </summary>
        public string mountPointsAttributeFile {
            get { return _mountPointsAttributeFile; }
            set {
                if (_mountPointsAttributeFile != value) {
                    _mountPointsAttributeFile = value;
                    if (_mountPointsAttributeFile == null) {
                        _mountPointsAttributeFile = MOUNTPOINTS_ATTRIB_DEFAULT_FILENAME;
                    }
                    isDirty = true;
                    ReloadMountPointsData();
                }
            }
        }

        /// <summary>
        /// Clears any mount point highlighted (color changed) and resets them to default city color (used from Editor)
        /// </summary>
        public void HideMountPointHighlights() {
            if (mountPointsLayer == null)
                return;
            Renderer[] rr = mountPointsLayer.GetComponentsInChildren<Renderer>(true);
            for (int k = 0; k < rr.Length; k++)
                rr[k].sharedMaterial = mountPointsMat;
        }

        /// <summary>
        /// Toggles the mount point highlight.
        /// </summary>
        /// <param name="mountPointIndex">Moint point index in the mount points collection.</param>
        /// <param name="color">Color.</param>
        /// <param name="highlighted">If set to <c>true</c> the color of the mount point will be changed. If set to <c>false</c> the color of the mount point will be reseted to default color</param>
        public void ToggleMountPointHighlight(int mountPointIndex, Color color, bool highlighted) {
            if (mountPointsLayer == null)
                return;
            Transform t = mountPointsLayer.transform.Find(mountPointIndex.ToString());
            if (t == null)
                return;
            Renderer rr = t.gameObject.GetComponent<Renderer>();
            if (rr == null)
                return;
            Material mat;
            if (highlighted) {
                mat = Instantiate(rr.sharedMaterial);
                mat.name = rr.sharedMaterial.name;
                mat.hideFlags = HideFlags.DontSave;
                mat.color = color;
                rr.sharedMaterial = mat;
            } else {
                rr.sharedMaterial = mountPointsMat;
            }
        }


        /// <summary>
        /// Returns an array with the mount points names.
        /// </summary>
        public string[] GetMountPointNames() {
            return GetMountPointNames(-1, -1);
        }

        /// <summary>
        /// Returns an array with the mount points names.
        /// </summary>
        public string[] GetMountPointNames(int countryIndex) {
            return GetMountPointNames(countryIndex, -1);
        }


        /// <summary>
        /// Returns an array with the mount points names.
        /// </summary>
        public string[] GetMountPointNames(int countryIndex, int provinceIndex) {
            List<string> c = new List<string>(20);
            for (int k = 0; k < mountPoints.Count; k++) {
                if ((mountPoints[k].countryIndex == countryIndex || countryIndex == -1) &&
                    (mountPoints[k].provinceIndex == provinceIndex || provinceIndex == -1)) {
                    c.Add(mountPoints[k].name + " (" + k + ")");
                }
            }
            c.Sort();
            return c.ToArray();
        }

        /// <summary>
        /// Returns the index of a mount point in the global mount points collection. Note that country index can be supplied due to repeated mount point names.
        /// Pass -1 to countryIndex to ignore filters.
        /// </summary>
        public int GetMountPointIndex(int countryIndex, string mountPointName) {
            return GetMountPointIndex(countryIndex, -1, mountPointName);
        }

        /// <summary>
        /// Returns the index of a mount point in the global mount points collection. Note that country (and optionally province) index can be supplied due to repeated mount point names.
        /// Pass -1 to countryIndex or provinceIndex to ignore filters.
        /// </summary>
        public int GetMountPointIndex(int countryIndex, int provinceIndex, string mountPointName) {
            if (mountPoints == null) return -1;
            int mountPointCount = mountPoints.Count;
            for (int k = 0; k < mountPointCount; k++) {
                if ((mountPoints[k].countryIndex == countryIndex || countryIndex == -1) &&
                    (mountPoints[k].provinceIndex == provinceIndex || provinceIndex == -1) &&
                    mountPoints[k].name.Equals(mountPointName)) {
                    return k;
                }
            }
            return -1;
        }


        /// <summary>
        /// Returns the mount point index by screen position.
        /// </summary>
        public bool GetMountPointIndex(Ray ray, out int mountPointIndex) {
            Vector3 hitPos;
            if (GetGlobeIntersection(ray, out hitPos)) {
                Vector3 localHit = transform.InverseTransformPoint(hitPos);
                int c = GetMountPointNearPoint(localHit);
                if (c >= 0) {
                    mountPointIndex = c;
                    return true;
                }
            }
            mountPointIndex = -1;
            return false;
        }


        /// <summary>
        /// Deletes all mount points of current selected country's continent
        /// </summary>
        public void MountPointsDeleteFromSameContinent(string continentName) {
            HideMountPointHighlights();
            int k = -1;
            while (++k < mountPoints.Count) {
                int cindex = mountPoints[k].countryIndex;
                if (cindex >= 0) {
                    string mpContinent = countries[cindex].continent;
                    if (mpContinent.Equals(continentName)) {
                        mountPoints.RemoveAt(k);
                        k--;
                    }
                }
            }
        }


        /// <summary>
        /// Returns a list of mount points that are visible (front facing camera)
        /// </summary>
        public List<MountPoint> GetVisibleMountPoints() {
            List<MountPoint> vc = new List<MountPoint>(30);
            if (mountPoints == null) return null;
            Camera cam = mainCamera;
            for (int k = 0; k < mountPoints.Count; k++) {
                MountPoint mp = mountPoints[k];

                // Check if city is facing camera
                Vector3 center = transform.TransformPoint(mp.localPosition);
                Vector3 dir = center - transform.position;
                float d = Vector3.Dot(cam.transform.forward, dir);
                if (d < -0.2f) {
                    // Check if city is inside viewport
                    Vector3 vpos = cam.WorldToViewportPoint(center);
                    float viewportMinX = cam.rect.xMin;
                    float viewportMaxX = cam.rect.xMax;
                    float viewportMinY = cam.rect.yMin;
                    float viewportMaxY = cam.rect.yMax;
                    if (vpos.x >= viewportMinX && vpos.x <= viewportMaxX && vpos.y >= viewportMinY && vpos.y <= viewportMaxY) {
                        vc.Add(mp);
                    }
                }
            }
            return vc;
        }


        /// <summary>
        /// Returns a list of mount points that are visible and located inside the rectangle defined by two given sphere points
        /// </summary>
        public List<MountPoint> GetVisibleMountPoints(Vector3 rectTopLeft, Vector3 rectBottomRight) {
            Vector2 latlon0, latlon1;
            latlon0 = Conversion.GetBillboardPosFromSpherePoint(rectTopLeft);
            latlon1 = Conversion.GetBillboardPosFromSpherePoint(rectBottomRight);
            Rect rect = new Rect(latlon0.x, latlon1.y, latlon1.x - latlon0.x, latlon0.y - latlon1.y);
            List<MountPoint> selectedMountPoints = new List<MountPoint>();

            int mpCount = mountPoints.Count;
            for (int k = 0; k < mpCount; k++) {
                MountPoint mp = mountPoints[k];
                Vector2 bpos = Conversion.GetBillboardPosFromSpherePoint(mp.localPosition);
                if (rect.Contains(bpos)) {
                    selectedMountPoints.Add(mp);
                }
            }
            return selectedMountPoints;
        }


        /// <summary>
        /// Returns mount points belonging to a provided country.
        /// </summary>
        /// <param name="countryIndex"></param>
        /// <param name="results">Provided by the user. The list will be filled with the results.</param>
        /// <returns>Number of mount points found.</returns>
        public int GetMountPoints(int countryIndex, List<MountPoint> results) {
            if (results == null) return 0;
            results.Clear();
            int mountPointCount = mountPoints.Count;
            for (int c = 0; c < mountPointCount; c++) {
                if (mountPoints[c].countryIndex == countryIndex)
                    results.Add(mountPoints[c]);
            }
            return results.Count;
        }

        /// <summary>
        /// Returns mount points belonging to a provided country and province.
        /// </summary>
        /// <param name="countryIndex"></param>
        /// <param name="provinceIndex"></param>
        /// <param name="results">Provided by the user. The list will be filled with the results.</param>
        /// <returns>Number of mount points found.</returns>
        public int GetMountPoints(int countryIndex, int provinceIndex, List<MountPoint> results) {
            if (results == null) return 0;
            results.Clear();
            int mountPointCount = mountPoints.Count;
            for (int c = 0; c < mountPointCount; c++) {
                if (mountPoints[c].countryIndex == countryIndex && mountPoints[c].provinceIndex == provinceIndex)
                    results.Add(mountPoints[c]);
            }
            return results.Count;
        }

        /// <summary>
        /// Returns mount points enclosed by a region.
        /// </summary>
        /// <param name="region"></param>
        /// <param name="results">Provided by the user. The list will be filled with the results.</param>
        /// <returns>Number of mount points found.</returns>
        public int GetMountPoints(Region region, List<MountPoint> results) {
            if (results == null) return 0;
            results.Clear();
            int mountPointCount = mountPoints.Count;
            for (int c = 0; c < mountPointCount; c++) {
                if (region.Contains(mountPoints[c].latlon)) {
                    results.Add(mountPoints[c]);
                }
            }
            return results.Count;
        }


        /// <summary>
        /// Returns a list of mount points whose attributes matches predicate
        /// </summary>
        public void GetMountPoints(AttribPredicate predicate, List<MountPoint> results) {
            if (results == null) return;
            int mountPointCount = mountPoints.Count;
            for (int k = 0; k < mountPointCount; k++) {
                MountPoint mountPoint = mountPoints[k];
                if (mountPoint.hasAttributes && predicate(mountPoint.attrib))
                    results.Add(mountPoint);
            }
        }


        /// <summary>
        /// Gets XML attributes of all mount points in jSON format.
        /// </summary>
        public string GetMountPointsAttributes(bool prettyPrint = true) {
            return GetMountPointsAttributes(new List<MountPoint>(mountPoints), prettyPrint);
        }

        /// <summary>
        /// Gets XML attributes of provided mount points in jSON format.
        /// </summary>
        public string GetMountPointsAttributes(List<MountPoint> mountPoints, bool prettyPrint = true) {
            JSONObject composed = new JSONObject();
            int mountPointCount = mountPoints.Count;
            for (int k = 0; k < mountPointCount; k++) {
                MountPoint mountPoint = mountPoints[k];
                if (mountPoint.hasAttributes && mountPoint.attrib.keys != null) {
                    composed.AddField(k.ToString(), mountPoint.attrib);
                }
            }
            return composed.Print(prettyPrint);
        }

        /// <summary>
        /// Sets mount points attributes from a jSON formatted string.
        /// </summary>
        public void SetMountPointsAttributes(string jSON) {
            JSONObject composed = new JSONObject(jSON);
            if (composed.keys == null)
                return;
            int keyCount = composed.keys.Count;
            for (int k = 0; k < keyCount; k++) {
                int mountPointIndex = int.Parse(composed.keys[k]);
                mountPoints[mountPointIndex].attrib = composed[k];
            }
        }



        #endregion


    }

}