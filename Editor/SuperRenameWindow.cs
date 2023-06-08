using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SuperRename
{
    public class SuperRenameWindow : EditorWindow
    {
        private TextField _patternField;
        private Toggle _useRegexToggle;
        private Toggle _caseSensitive;
        private TextField _renameField;
        private Label _noSelected;
        private ListView _renameList;
        private Button _applyButton;

        private readonly List<Object> _objects = new List<Object>();

        private static SuperRenamer _superRenamer;
        private Toggle _matchAllToggle;
        private Image _warningImage;
        

        [MenuItem("Assets/Super Rename", false, 18)]
        public static void Open()
        {
            SuperRenameWindow wnd = GetWindow<SuperRenameWindow>();
            wnd.titleContent = new GUIContent("Super Rename");
            wnd.minSize = new Vector2(300, 400);
        }

        public void CreateGUI()
        {
            _superRenamer ??= new SuperRenamer();
            
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/ru.nikitazyz.super-rename/Editor/SuperRenameWindow.uxml");
            var visualTreeInstance = visualTree.Instantiate();
            visualTreeInstance.StretchToParentSize();
            root.Add(visualTreeInstance);

            _patternField = visualTreeInstance.Q<TextField>("Pattern");
            _warningImage = visualTreeInstance.Q<Image>("WarningImage");
            _useRegexToggle = visualTreeInstance.Q<Toggle>("Regex");
            _caseSensitive = visualTreeInstance.Q<Toggle>("CaseSens");
            _matchAllToggle = visualTreeInstance.Q<Toggle>("MatchAll");
            _renameField = visualTreeInstance.Q<TextField>("Rename");
            _noSelected = visualTreeInstance.Q<Label>("NoSelected");
            _renameList = visualTreeInstance.Q<ListView>("RenameList");
            _applyButton = visualTreeInstance.Q<Button>("ApplyButton");
            
            if (_superRenamer != null)
            {
                _patternField.value = _superRenamer.Pattern;
                _useRegexToggle.value = _superRenamer.UseRegex;
                _caseSensitive.value = _superRenamer.CaseSensitive;
                _matchAllToggle.value = _superRenamer.MatchAll;
                _renameField.value = _superRenamer.RenameTo;
            }

            _patternField.RegisterValueChangedCallback(evt =>
            {
                _superRenamer.Pattern = evt.newValue;
                _renameList.Rebuild();
                CheckPattern();
            });
            CheckPattern();

            _useRegexToggle.RegisterValueChangedCallback(evt =>
            {
                _superRenamer.UseRegex = evt.newValue;
                _renameList.Rebuild();
                CheckPattern();
            });

            _caseSensitive.RegisterValueChangedCallback(evt =>
            {
                _superRenamer.CaseSensitive = evt.newValue;
                _renameList.Rebuild();
            });

            _matchAllToggle.RegisterValueChangedCallback(evt =>
            {
                _superRenamer.MatchAll = evt.newValue;
                _renameList.Rebuild();
            });

            _renameField.RegisterValueChangedCallback(evt =>
            {
                _superRenamer.RenameTo = evt.newValue;
                _renameList.Rebuild();
            });
            
            _applyButton.clicked += ApplyButtonOnClick;

            Selection.selectionChanged += SelectionChanged;

            _renameList.itemsSource = _objects;
            _renameList.makeItem += () =>
            {
                VisualElement visualElement = new VisualElement();
                visualElement.AddToClassList("selected-objects-item");
                Label currentNameField = new Label
                {
                    name = "CurrentName"
                };
                Label newNameField = new Label
                {
                    name = "NewName"
                };

                visualElement.Add(currentNameField);
                visualElement.Add(newNameField);

                return visualElement;
            };

            _renameList.bindItem += (element, i) =>
            {
                var currentNameField = element.Q<Label>("CurrentName");
                var newNameField = element.Q<Label>("NewName");
                currentNameField.text = $"{_objects[i].name} <color=#808080ff>({ObjectNames.NicifyVariableName(_objects[i].GetType().Name)})</color>";
                if (SuperRenamer.IsPatternValid(_superRenamer.Pattern))
                {
                    var newName = _superRenamer.Rename(i, _objects[i].name);
                    if (string.IsNullOrEmpty(newName) || newName == _objects[i].name)
                    {
                        newNameField.text = $"<color=#808080ff><noparse>{_objects[i].name}</noparse></color>";
                        return;
                    }

                    newNameField.text = "<noparse>"+newName+"</noparse>";
                }
            };

            SelectionChanged();
        }

        private void ApplyButtonOnClick()
        {
            
            Dictionary<Object, string> newNames = new Dictionary<Object, string>();
            Dictionary<string, string> newNamesAssets = new Dictionary<string, string>();

            for (var i = 0; i < _objects.Count; i++)
            {
                var o = _objects[i];
                if (!SuperRenamer.IsPatternValid(_superRenamer.Pattern))
                {
                    continue;
                }

                var newName = _superRenamer.Rename(i, o.name);
                if (string.IsNullOrEmpty(newName) || newName == o.name)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path))
                {
                    newNames.Add(o, newName);
                }
                else
                {
                    newNamesAssets.Add(path, newName);
                }
                
            }

            if (newNames.Count + newNamesAssets.Count == 0)
            {
                return;
            }

            bool dialog = EditorUtility.DisplayDialog("",$"Do you want to rename {newNames.Count + newNamesAssets.Count} objects?", "Yes", "No");
            if (dialog)
            {
                foreach (var newName in newNames)
                {
                    newName.Key.name = newName.Value;
                    AssetDatabase.SaveAssets();
                    _renameList.Rebuild();
                }

                foreach (var newName in newNamesAssets)
                {
                    AssetDatabase.RenameAsset(newName.Key, newName.Value);
                }
            }
        }

        private void CheckPattern()
        {
            SetWarning(null);
            if (!_superRenamer.UseRegex || _superRenamer.Pattern == null)
            {
                return;
            }
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                Regex.Match("", _superRenamer.Pattern);
            }
            catch (ArgumentException e)
            {
                SetWarning(e.Message);
            }
        }

        private void SetWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                _warningImage.style.display = DisplayStyle.None;
                return;
            }
            _warningImage.style.display = DisplayStyle.Flex;
            _warningImage.tooltip = message;
        }

        private void SelectionChanged()
        {
            _noSelected.style.display = Selection.count != 0 ? DisplayStyle.None: DisplayStyle.Flex;
            _objects.Clear();
            _objects.AddRange(Selection.objects);
            _renameList.Rebuild();
        }
    }
}