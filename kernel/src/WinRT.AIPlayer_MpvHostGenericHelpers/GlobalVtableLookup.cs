using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ABI.Microsoft.UI.Composition;
using ABI.Microsoft.UI.Xaml;
using ABI.System;
using ABI.System.Collections;
using ABI.System.Collections.Generic;
using ABI.System.Collections.Specialized;
using ABI.System.ComponentModel;

namespace WinRT.AIPlayer_MpvHostGenericHelpers;

internal static class GlobalVtableLookup
{
	[ModuleInitializer]
	internal static void InitializeGlobalVtableLookup()
	{
		ComWrappersSupport.RegisterTypeComInterfaceEntriesLookup(LookupVtableEntries);
		ComWrappersSupport.RegisterTypeRuntimeClassNameLookup(LookupRuntimeClassName);
	}

	private static ComWrappers.ComInterfaceEntry[] LookupVtableEntries(System.Type type)
	{
		switch (type.ToString())
		{
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuSearchApiGroup]":
				_ = IReadOnlyList_System_ComponentModel_INotifyPropertyChanged.Initialized;
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_System_ComponentModel_INotifyPropertyChanged.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[6]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<INotifyPropertyChanged>.IID,
					Vtable = IReadOnlyListMethods<INotifyPropertyChanged>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<INotifyPropertyChanged>.IID,
					Vtable = IEnumerableMethods<INotifyPropertyChanged>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.Generic.List`1[WinUISample.Models.OverlayTrackOption]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.MediaSegment]":
			case "System.Collections.Generic.List`1[WinUISample.Services.DanmakuMatchResult]":
			case "System.Collections.Generic.List`1[WinUISample.Models.OverlayVersionOption]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.OverlayTrackOption]":
			case "System.Collections.Generic.List`1[WinUISample.Models.EpisodeListItem]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Controls.SubtitleResult]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.OverlayVersionOption]":
			case "System.Collections.Generic.List`1[WinUISample.Models.MediaSegment]":
			case "System.Collections.Generic.List`1[WinUISample.Models.DanmakuApiEntry]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.ViewModels.PlayerViewModel+MpvSubtitleTrack]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[Richasy.Danmaku.Models.DanmakuItem]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.TodbChapter]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuSearchAnime]":
			case "System.Collections.Generic.List`1[WinUISample.ViewModels.PlayerViewModel+MpvSubtitleTrack]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Services.DanmakuMatchResult]":
			case "System.Collections.Generic.List`1[WinUISample.Controls.SubtitleResult]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuApiEntry]":
			case "System.Collections.Generic.List`1[WinUISample.Models.TodbChapter]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.EpisodeListItem]":
			case "System.Collections.Generic.List`1[Richasy.Danmaku.Models.DanmakuItem]":
			case "System.Collections.Generic.List`1[WinUISample.Models.DanmakuSearchAnime]":
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[4]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "WinUIEx.Icon":
				return new ComWrappers.ComInterfaceEntry[1]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.Generic.KeyValuePair`2[System.String,System.String]":
				_ = KeyValuePair_string_string.Initialized;
				return new ComWrappers.ComInterfaceEntry[1]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = KeyValuePairMethods<string, string>.IID,
					Vtable = KeyValuePairMethods<string, string>.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Object]":
				_ = IEnumerator_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[2]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<object>.IID,
					Vtable = IEnumeratorMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "FluentIcons.WinUI.SymbolIcon":
				return new ComWrappers.ComInterfaceEntry[5]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IUIElementOverridesMethods.IID,
					Vtable = IUIElementOverridesMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IAnimationObjectMethods.IID,
					Vtable = IAnimationObjectMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IVisualElementMethods.IID,
					Vtable = IVisualElementMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IVisualElement2Methods.IID,
					Vtable = IVisualElement2Methods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IFrameworkElementOverridesMethods.IID,
					Vtable = IFrameworkElementOverridesMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,System.String[]]":
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,System.String[]]":
			case "System.Collections.Generic.Dictionary`2[System.String,Richasy.MpvKernel.MpvNode]":
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,Richasy.MpvKernel.MpvNode]":
			case "System.Collections.Generic.Dictionary`2[System.String,System.String[]]":
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,Richasy.MpvKernel.MpvNode]":
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[2]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.ObjectModel.ObservableCollection`1[System.String]":
				_ = IList_string.Initialized;
				_ = IReadOnlyList_string.Initialized;
				_ = IEnumerable_char.Initialized;
				_ = IReadOnlyList_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerable_object.Initialized;
				_ = IReadOnlyList_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IReadOnlyList_System_Collections_IEnumerable.Initialized;
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_string.Initialized;
				_ = IEnumerable_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerable_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IEnumerable_System_Collections_IEnumerable.Initialized;
				return new ComWrappers.ComInterfaceEntry[15]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods<string>.IID,
					Vtable = IListMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<string>.IID,
					Vtable = IReadOnlyListMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable<char>>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable<object>>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<string>.IID,
					Vtable = IEnumerableMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable<char>>.IID,
					Vtable = IEnumerableMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable<object>>.IID,
					Vtable = IEnumerableMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable>.IID,
					Vtable = IEnumerableMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyCollectionChangedMethods.IID,
					Vtable = INotifyCollectionChangedMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyPropertyChangedMethods.IID,
					Vtable = INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[System.String]":
			case "System.Collections.Generic.List`1[System.String]":
				_ = IList_string.Initialized;
				_ = IReadOnlyList_string.Initialized;
				_ = IEnumerable_char.Initialized;
				_ = IReadOnlyList_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerable_object.Initialized;
				_ = IReadOnlyList_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IReadOnlyList_System_Collections_IEnumerable.Initialized;
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_string.Initialized;
				_ = IEnumerable_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerable_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IEnumerable_System_Collections_IEnumerable.Initialized;
				return new ComWrappers.ComInterfaceEntry[13]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods<string>.IID,
					Vtable = IListMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<string>.IID,
					Vtable = IReadOnlyListMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable<char>>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable<object>>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<IEnumerable>.IID,
					Vtable = IReadOnlyListMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<string>.IID,
					Vtable = IEnumerableMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable<char>>.IID,
					Vtable = IEnumerableMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable<object>>.IID,
					Vtable = IEnumerableMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<IEnumerable>.IID,
					Vtable = IEnumerableMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.IEnumerable]":
				_ = IEnumerator_System_Collections_IEnumerable.Initialized;
				return new ComWrappers.ComInterfaceEntry[2]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable>.IID,
					Vtable = IEnumeratorMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.Specialized.SingleItemReadOnlyList":
			case "System.Collections.Specialized.ReadOnlyList":
				return new ComWrappers.ComInterfaceEntry[2]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "WinUISample.Services.DanmakuApiMatchResult[]":
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[4]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.IEnumerable`1[System.Object]]":
				_ = IEnumerable_object.Initialized;
				_ = IEnumerator_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IEnumerator_System_Collections_IEnumerable.Initialized;
				return new ComWrappers.ComInterfaceEntry[3]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable<object>>.IID,
					Vtable = IEnumeratorMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable>.IID,
					Vtable = IEnumeratorMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.ObjectModel.ObservableCollection`1[WinUISample.Models.DanmakuSearchApiGroup]":
				_ = IReadOnlyList_System_ComponentModel_INotifyPropertyChanged.Initialized;
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_System_ComponentModel_INotifyPropertyChanged.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[8]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<INotifyPropertyChanged>.IID,
					Vtable = IReadOnlyListMethods<INotifyPropertyChanged>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<INotifyPropertyChanged>.IID,
					Vtable = IEnumerableMethods<INotifyPropertyChanged>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyCollectionChangedMethods.IID,
					Vtable = INotifyCollectionChangedMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyPropertyChangedMethods.IID,
					Vtable = INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.Generic.Dictionary`2[System.String,System.String]":
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,System.String]":
				_ = IDictionary_string_string.Initialized;
				_ = IReadOnlyDictionary_string_string.Initialized;
				_ = KeyValuePair_string_string.Initialized;
				_ = IEnumerable_System_Collections_Generic_KeyValuePair_string__string_.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[5]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDictionaryMethods<string, string>.IID,
					Vtable = IDictionaryMethods<string, string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyDictionaryMethods<string, string>.IID,
					Vtable = IReadOnlyDictionaryMethods<string, string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<System.Collections.Generic.KeyValuePair<string, string>>.IID,
					Vtable = IEnumerableMethods<System.Collections.Generic.KeyValuePair<string, string>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.KeyValuePair`2[System.String,System.String]]":
				_ = KeyValuePair_string_string.Initialized;
				_ = IEnumerator_System_Collections_Generic_KeyValuePair_string__string_.Initialized;
				_ = IEnumerator_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[3]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<System.Collections.Generic.KeyValuePair<string, string>>.IID,
					Vtable = IEnumeratorMethods<System.Collections.Generic.KeyValuePair<string, string>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<object>.IID,
					Vtable = IEnumeratorMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.String]":
				_ = IEnumerator_string.Initialized;
				_ = IEnumerable_char.Initialized;
				_ = IEnumerator_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerable_object.Initialized;
				_ = IEnumerator_System_Collections_Generic_IEnumerable_object_.Initialized;
				_ = IEnumerator_System_Collections_IEnumerable.Initialized;
				_ = IEnumerator_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[6]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<string>.IID,
					Vtable = IEnumeratorMethods<string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable<char>>.IID,
					Vtable = IEnumeratorMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable<object>>.IID,
					Vtable = IEnumeratorMethods<IEnumerable<object>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable>.IID,
					Vtable = IEnumeratorMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<object>.IID,
					Vtable = IEnumeratorMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "System.Collections.ObjectModel.ObservableCollection`1[WinUISample.Models.OverlayTrackOption]":
				_ = IReadOnlyList_object.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[6]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyListMethods<object>.IID,
					Vtable = IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyCollectionChangedMethods.IID,
					Vtable = INotifyCollectionChangedMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = INotifyPropertyChangedMethods.IID,
					Vtable = INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.IEnumerable`1[System.Char]]":
				_ = IEnumerable_char.Initialized;
				_ = IEnumerator_System_Collections_Generic_IEnumerable_char_.Initialized;
				_ = IEnumerator_System_Collections_IEnumerable.Initialized;
				return new ComWrappers.ComInterfaceEntry[3]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable<char>>.IID,
					Vtable = IEnumeratorMethods<IEnumerable<char>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<IEnumerable>.IID,
					Vtable = IEnumeratorMethods<IEnumerable>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,System.String]":
				_ = IReadOnlyDictionary_string_string.Initialized;
				_ = KeyValuePair_string_string.Initialized;
				_ = IEnumerable_System_Collections_Generic_KeyValuePair_string__string_.Initialized;
				_ = IEnumerable_object.Initialized;
				return new ComWrappers.ComInterfaceEntry[4]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IReadOnlyDictionaryMethods<string, string>.IID,
					Vtable = IReadOnlyDictionaryMethods<string, string>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<System.Collections.Generic.KeyValuePair<string, string>>.IID,
					Vtable = IEnumerableMethods<System.Collections.Generic.KeyValuePair<string, string>>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods<object>.IID,
					Vtable = IEnumerableMethods<object>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
				};
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.ComponentModel.INotifyPropertyChanged]":
				_ = IEnumerator_System_ComponentModel_INotifyPropertyChanged.Initialized;
				return new ComWrappers.ComInterfaceEntry[2]
				{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumeratorMethods<INotifyPropertyChanged>.IID,
					Vtable = IEnumeratorMethods<INotifyPropertyChanged>.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
				};
			default:
				return null;
		}
	}

	private static string LookupRuntimeClassName(System.Type type)
	{
		switch (type.ToString())
		{
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuSearchApiGroup]":
			case "System.Collections.ObjectModel.ObservableCollection`1[WinUISample.Models.DanmakuSearchApiGroup]":
				return "Windows.Foundation.Collections.IVectorView`1<Microsoft.UI.Xaml.Data.INotifyPropertyChanged>";
			case "System.Collections.Generic.List`1[WinUISample.Models.OverlayTrackOption]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.MediaSegment]":
			case "System.Collections.Generic.List`1[WinUISample.Services.DanmakuMatchResult]":
			case "System.Collections.Generic.List`1[WinUISample.Models.OverlayVersionOption]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.OverlayTrackOption]":
			case "System.Collections.Generic.List`1[WinUISample.Models.EpisodeListItem]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Controls.SubtitleResult]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.OverlayVersionOption]":
			case "System.Collections.Generic.List`1[WinUISample.Models.MediaSegment]":
			case "System.Collections.Generic.List`1[WinUISample.Models.DanmakuApiEntry]":
			case "System.Collections.ObjectModel.ObservableCollection`1[WinUISample.Models.OverlayTrackOption]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.ViewModels.PlayerViewModel+MpvSubtitleTrack]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[Richasy.Danmaku.Models.DanmakuItem]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.TodbChapter]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuSearchAnime]":
			case "System.Collections.Generic.List`1[WinUISample.ViewModels.PlayerViewModel+MpvSubtitleTrack]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Services.DanmakuMatchResult]":
			case "System.Collections.Generic.List`1[WinUISample.Controls.SubtitleResult]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.DanmakuApiEntry]":
			case "System.Collections.Generic.List`1[WinUISample.Models.TodbChapter]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[WinUISample.Models.EpisodeListItem]":
			case "System.Collections.Generic.List`1[Richasy.Danmaku.Models.DanmakuItem]":
			case "System.Collections.Generic.List`1[WinUISample.Models.DanmakuSearchAnime]":
				return "Windows.Foundation.Collections.IVectorView`1<Object>";
			case "WinUIEx.Icon":
				return "Windows.Foundation.IClosable";
			case "System.Collections.Generic.KeyValuePair`2[System.String,System.String]":
				return "Windows.Foundation.Collections.IKeyValuePair`2<String, String>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Object]":
				return "Windows.Foundation.Collections.IIterator`1<Object>";
			case "FluentIcons.WinUI.SymbolIcon":
				return "Microsoft.UI.Xaml.IUIElementOverrides";
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,System.String[]]":
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,System.String[]]":
			case "System.Collections.Generic.Dictionary`2[System.String,Richasy.MpvKernel.MpvNode]":
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,Richasy.MpvKernel.MpvNode]":
			case "System.Collections.Generic.Dictionary`2[System.String,System.String[]]":
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,Richasy.MpvKernel.MpvNode]":
				return "Windows.Foundation.Collections.IIterable`1<Object>";
			case "System.Collections.ObjectModel.ObservableCollection`1[System.String]":
			case "System.Collections.ObjectModel.ReadOnlyCollection`1[System.String]":
			case "System.Collections.Generic.List`1[System.String]":
				return "Windows.Foundation.Collections.IVector`1<String>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.IEnumerable]":
				return "Windows.Foundation.Collections.IIterator`1<Microsoft.UI.Xaml.Interop.IBindableIterable>";
			case "System.Collections.Specialized.SingleItemReadOnlyList":
			case "WinUISample.Services.DanmakuApiMatchResult[]":
			case "System.Collections.Specialized.ReadOnlyList":
				return "Microsoft.UI.Xaml.Interop.IBindableVector";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.IEnumerable`1[System.Object]]":
				return "Windows.Foundation.Collections.IIterator`1<Windows.Foundation.Collections.IIterable`1<Object>>";
			case "System.Collections.Generic.Dictionary`2[System.String,System.String]":
			case "System.Collections.ObjectModel.ReadOnlyDictionary`2[System.String,System.String]":
				return "Windows.Foundation.Collections.IMap`2<String, String>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.KeyValuePair`2[System.String,System.String]]":
				return "Windows.Foundation.Collections.IIterator`1<Windows.Foundation.Collections.IKeyValuePair`2<String, String>>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.String]":
				return "Windows.Foundation.Collections.IIterator`1<String>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.Collections.Generic.IEnumerable`1[System.Char]]":
				return "Windows.Foundation.Collections.IIterator`1<Windows.Foundation.Collections.IIterable`1<Char>>";
			case "ABI.System.Collections.Generic.ConstantSplittableMap`2[System.String,System.String]":
				return "Windows.Foundation.Collections.IMapView`2<String, String>";
			case "ABI.System.Collections.Generic.ToAbiEnumeratorAdapter`1[System.ComponentModel.INotifyPropertyChanged]":
				return "Windows.Foundation.Collections.IIterator`1<Microsoft.UI.Xaml.Data.INotifyPropertyChanged>";
			default:
				return null;
		}
	}
}
