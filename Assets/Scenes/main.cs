using UnityEngine;
using System.Text;
using System.Collections.Generic;
using WorldMapStrategyKit;


namespace WorldMapStrategyKit
{
	public class testing : MonoBehaviour
	{

		WMSK map;

		void Start()
		{
			// Get a reference to the World Map API:
			map = WMSK.instance;


			/* Register events: this is optionally but allows your scripts to be informed instantly as the mouse enters or exits a country, province or city */
			map.OnCityEnter += (int cityIndex) => Debug.Log("Entered city " + map.cities[cityIndex].name);
			map.OnCityExit += (int cityIndex) => Debug.Log("Exited city " + map.cities[cityIndex].name);
			map.OnCityClick += (int cityIndex, int buttonIndex) => Debug.Log("Clicked city " + map.cities[cityIndex].name);
			map.OnCountryEnter += (int countryIndex, int regionIndex) => Debug.Log("Entered country " + map.countries[countryIndex].name);
			map.OnCountryExit += (int countryIndex, int regionIndex) => Debug.Log("Exited country " + map.countries[countryIndex].name);
			map.OnCountryClick += (int countryIndex, int regionIndex, int buttonIndex) => Debug.Log("Clicked country " + map.countries[countryIndex].name);
			map.OnProvinceEnter += (int provinceIndex, int regionIndex) => Debug.Log("Entered province " + map.provinces[provinceIndex].name);
			map.OnProvinceExit += (int provinceIndex, int regionIndex) => Debug.Log("Exited province " + map.provinces[provinceIndex].name);
			map.OnProvinceClick += (int provinceIndex, int regionIndex, int buttonIndex) => Debug.Log("Clicked province " + map.provinces[provinceIndex].name);

			map.CenterMap();


			for (int colorizeIndex = 0; colorizeIndex < map.countries.Length; colorizeIndex++)
			{
				//if (map.countries[colorizeIndex].continent.Equals("Europe
				Color color = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
				map.ToggleCountrySurface(map.countries[colorizeIndex].name, true, color);
			}
		}

		void Update()
		{
		}

	}

}

