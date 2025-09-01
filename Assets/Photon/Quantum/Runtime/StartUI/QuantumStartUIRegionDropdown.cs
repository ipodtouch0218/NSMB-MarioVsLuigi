namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Linq;
  using System.Globalization;
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Dropdown = TMPro.TMP_Dropdown;
#else
  using Dropdown = UnityEngine.UI.Dropdown;
#endif
  using UnityEngine;
  using UnityEngine.EventSystems;

  /// <summary>
  /// A specialized dropdown component that fetches regions from the cloud and allows selection of a region.
  /// </summary>
  public partial class QuantumStartUIRegionDropdown : Dropdown {
    /// <summary>
    /// Enable or disable the runtime fetching of regions from the cloud.
    /// </summary>
    public bool FetchRegionsFromCloud = true;
    /// <summary>
    /// A callback to run when the region fetching starts.
    /// </summary>
    public Action OnFetchingStart;
    /// <summary>
    /// A callback to run when the region fetching ends.
    /// </summary>
    public Action OnFetchingEnd;

    List<OptionData> _cachedRegions;

    /// <summary>
    /// This method is implemented by the Photon SDK to actually provide the Task that fetches the available regions.
    /// Partial methods do not allow async or non-void return types, hence the indirection with the Task parameter.
    /// </summary>
    /// <param name="regionListPromise">The actual task that fetches the regions.</param>
    partial void GetAvailableRegionsTaskUser(ref System.Threading.Tasks.Task<List<string>> regionListPromise);


    /// <summary>
    /// Adding a public method to select a certain value in the dropdown.
    /// The base implementation do not provide this.
    /// </summary>
    /// <param name="value">The value to set when found</param>
    /// <param name="addIfNotFound">Add the value to the options if not found</param>
    public void SelectValue(string value, bool addIfNotFound) {
      var index = options.FindIndex(o => o.text == value);
      if (index >= 0) {
        SetValueWithoutNotify(index);
      } else if (addIfNotFound) {
        options.Add(new OptionData { text = value });
        SetValueWithoutNotify(options.Count - 1);
      }
    }

    /// <summary>
    /// Get the currently selected value from the dropdown.
    /// </summary>
    /// <returns></returns>
    public string GetValue() {
       return options[value].text;
    }

    /// <summary>
    /// Overload the OnPointerClick method to commence fetching the regions from the Photon cloud.
    /// </summary>
    public override async void OnPointerClick(PointerEventData eventData) {
      if (interactable == false) {
        return;
      }

      if (FetchRegionsFromCloud == false) {
        Show();
        return;
      }

      var currentSelectedValue = default(string);
      if (options.Count > 0) {
        currentSelectedValue = options[value].text;
      }

      if (_cachedRegions == null || _cachedRegions.Count == 0) {
        var backupRegions = new List<OptionData>(options);
        ClearOptions();
        options.Add(new OptionData { text = "Fetching Regions.." });
        RefreshShownValue();
        try {
          OnFetchingStart?.Invoke();
          _cachedRegions = (await GetAvailableRegionsAsync()).Select(s => new OptionData(s)).ToList();
          _cachedRegions.Sort((a, b) => string.Compare(a.text, b.text, CultureInfo.InvariantCulture, CompareOptions.Ordinal));
          _cachedRegions.Insert(0, new OptionData { text = "Best Region" });
        } catch (Exception e) {
          Debug.LogError($"Failed to fetch regions: {e.Message}");
          _cachedRegions = backupRegions;
        } finally {
          OnFetchingEnd?.Invoke();
        }
      }

      ClearOptions();
      AddOptions(_cachedRegions);

      if (string.IsNullOrEmpty(currentSelectedValue) == false) {
        SelectValue(currentSelectedValue, addIfNotFound: false);
      }

      Show();
    }

    /// <summary>
    /// Boilerplate code to run the partial methods that implements fetching the regions from the cloud.
    /// </summary>
    /// <returns></returns>
    protected virtual async System.Threading.Tasks.Task<List<string>> GetAvailableRegionsAsync() {
      var regionListPromise = default(System.Threading.Tasks.Task<List<string>>);
      GetAvailableRegionsTaskUser(ref regionListPromise);
      if (regionListPromise != null) {
        return await regionListPromise;
      }
      return null;
    }
  }
}
