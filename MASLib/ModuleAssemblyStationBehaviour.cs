using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MASLib
{
    public class ModuleAssemblyStationBehaviour : MonoBehaviour
    {
        //This disables an annoying IDE warning
#pragma warning disable 0649
        [SerializeField] private Light roomLight;
        [SerializeField] private Light emergencyLight;
        [SerializeField] private KMGameplayRoom GameplayRoom;
        [SerializeField] private KMAudio Audio;
        [SerializeField] private KMBombInfo BombInfo;
        [SerializeField] private GameObject DefaultBackground;
        [SerializeField] private GameObject NeedyBackground;
        [SerializeField] private GameObject SpawnPoint;
        [SerializeField] private Animator ArmAnimator;
        [SerializeField] private Animator BeltAnimator;
        [SerializeField] private TextMesh DisplayText;
#pragma warning restore 0649
        
        private bool _initialSwitchOn = true;
        private bool _emergencyLightEnabled;
        private List<KtaneModule> _moduleGameobjects;
        private Coroutine _currentTextCoroutine;

        private const string _version = "1.1";

        private void Awake()
        {
            GameplayRoom.OnLightChange = OnLightChange;
        }

        private void Start()
        {
            DebugLog("Running version {0}", _version);
            StartCoroutine(ActivateEmergencyLight());
            //The OnLightChange event isn't triggered in the editor so I just trigger it manually
            if (Application.isEditor)
            {
                StartCoroutine(Setup());
            }
        }

        private void Update()
        {
            CheckWarningLight();
        }

        private IEnumerator Setup()
        {
            yield return new WaitForSeconds(1f);
            DebugLog("Setting up");
            var modules = new List<KtaneModule>();

            var moddedModules = FindObjectsOfType<KMBombModule>();
            var hiddenModules = new List<MysteryModuleInfo>();
            //Check if there is a Mystery Module on the bomb, since we don't want to reveal the hidden module
            if (moddedModules.Any(x => x.ModuleType == "mysterymodule"))
            {
                DebugLog("Mystery module found");
                const string componentName = "MysteryModuleScript";
                //There might be multiple, so we get all of them.
                var mysteryModules = moddedModules.Where(x => x.ModuleType == "mysterymodule").ToList();
                //Loop through all of them and get their hidden module using reflection
                for (var i = 0; i < mysteryModules.Count; i++)
                {
                    var mysteryModule = mysteryModules[i];
                    var comp = mysteryModule.gameObject.GetComponent(componentName);
                    var fldModule = comp.GetType().GetField("mystifiedModule", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fldIsSolved = comp.GetType().GetField("moduleSolved", BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (fldModule is null || fldIsSolved is null)
                    {
                        if (fldModule is null && fldIsSolved is null)
                        {
                            throw new MissingFieldException("both fields null in Setup()");
                        }

                        if (fldModule is null)
                        {
                            throw new MissingFieldException("fldmodule null in Setup()");
                        }
                        
                        throw new MissingFieldException("fldIsSolved fields null in Setup()");
                    }
                    
                    var mystifiedModule = (KMBombModule)fldModule.GetValue(comp);
                    if (mystifiedModule is null)
                    {
                        //For some reason mystery module didn't feel like assigning a hidden module. So we just ignore it.
                        continue;
                    }
                    
                    hiddenModules.Add(new MysteryModuleInfo(i, mystifiedModule.gameObject, fldIsSolved, comp, mystifiedModule.ModuleType));
                    DebugLog("Added a mystified module {0}", mystifiedModule.ModuleType);
                }
            }
            
            foreach (var module in moddedModules)
            {
                //If there is a mystery module the following if will run
                if (hiddenModules.Any())
                {
                    var hiddenModule = hiddenModules.SingleOrDefault(x => x.Module == module.gameObject);
                    //This module is the hidden one since the SingleOrDefault returned a value
                    if (hiddenModule != null)
                    {
                        modules.Add(new KtaneModule(ModuleType.Regular, module.gameObject, module.ModuleDisplayName, true, hiddenModule.Index));
                        continue;
                    }
                }
                //This module doesn't have anything to do with mystery module
                modules.Add(new KtaneModule(ModuleType.Regular, module.gameObject, module.ModuleDisplayName));
            }

            foreach (var module in FindObjectsOfType<KMNeedyModule>())
            {
                modules.Add(new KtaneModule(ModuleType.Needy, module.gameObject, module.ModuleDisplayName));
            }
            
            //Vanilla mods don't have the KMBombModule Component, so we need to get them manually
            foreach (var module in FindObjectsOfType<BombComponent>())
            {
                var componentType = (int)module.ComponentType;
                string moduleName;
            
                switch (componentType)
                {
                    case 2: moduleName = "Wires"; break;
                    case 3: moduleName = "The Button"; break;
                    case 4: moduleName = "Keypad"; break;
                    case 5: moduleName = "Simon Says"; break;
                    case 6: moduleName = "Who's on First"; break;
                    case 7: moduleName = "Memory"; break;
                    case 8: moduleName = "Morse Code"; break;
                    case 9: moduleName = "Complicated Wires"; break;
                    case 10: moduleName = "Wire Sequence"; break;
                    case 11: moduleName = "Maze"; break;
                    case 12: moduleName = "Password"; break;
                    case 13: moduleName = "Venting Gas"; break;
                    case 14: moduleName = "Capacitor Discharge"; break;
                    case 15: moduleName = "Knob"; break;
                    default: continue; //The Component is something else, such as the timer or an empty space.
                }
                    
                modules.Add(new KtaneModule(componentType > 12 ? ModuleType.Needy : ModuleType.Regular, module.gameObject, moduleName));
            }

            if (modules.Any())
            {
                _moduleGameobjects = modules;
                //Run this code if there is a mystery module
                if (hiddenModules.Any())
                {
                    StartCoroutine(MysModSolvedChecker(hiddenModules));
                }

                StartCoroutine(HandleConveyor());
                yield break;
            }

            throw new InvalidOperationException("No modules found, this should not happen. Please send a bug report!");
        }

        private void CheckWarningLight()
        {
            var lightEnabled = BombInfo.GetTime() < 60;
            emergencyLight.gameObject.SetActive(lightEnabled);
            _emergencyLightEnabled = lightEnabled;
        }

        private IEnumerator MysModSolvedChecker(List<MysteryModuleInfo> mysteryModuleInfos)
        {
            //If the module gets revealed, we want to be able to assemble it. This can only happen once mystery module solves,
            //so we just check if the mystery module associated has solved once every second, and if it has, mark the module as not hidden.
            var infos = mysteryModuleInfos;
            var remove = new List<MysteryModuleInfo>();
            while (infos.Any())
            {
                remove.Clear();
                foreach (var moduleInfo in infos)
                {
                    if (moduleInfo.IsSolved)
                    {
                        _moduleGameobjects.Single(x => x.MysModIndex == moduleInfo.Index).MysModHidden = false;
                        remove.Add(moduleInfo);
                        DebugLog("Module {0} with index {1} was solved, removing it from the mystified pool.", moduleInfo.ModuleType, moduleInfo.Index);
                    }
                }

                infos.RemoveAll(x => remove.Contains(x));
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator ActivateEmergencyLight()
        {
            //Emergency light once the timer reaches below one minute
            while (true)
            {
                while (_emergencyLightEnabled)
                {
                    emergencyLight.enabled = true;
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.EmergencyAlarm, transform);
                    yield return new WaitForSeconds(1.3f);
                    emergencyLight.enabled = false;
                    yield return new WaitForSeconds(2.5f);
                }

                yield return null;
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private IEnumerator HandleConveyor()
        {
            DebugLog("Starting conveyor handler.");
            //Holds all of the conveyor code
            while (true)
            {
                //Select a module that isn't hidden
                var selectedModule = _moduleGameobjects.Where(x => !x.MysModHidden).PickRandom();
                yield return new WaitForSeconds(3f);
                if (_currentTextCoroutine != null)
                {
                    StopCoroutine(_currentTextCoroutine);
                }
                
                //Set the computer display
                _currentTextCoroutine = StartCoroutine(HandleText(TextState.Preparing, selectedModule));
                //Set the background
                GameObject background; 
                switch (selectedModule.Type)
                {
                    case ModuleType.Needy:
                        background = NeedyBackground;
                        break;
                    case ModuleType.Regular:
                        background = DefaultBackground;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown module type");
                }
                
                background.SetActive(true);
                //I have no idea why, but for some godforsaken reason the stupid highlights dissapear on the original module unless we do this terribleness.
                var highlights = selectedModule.GameObject.GetComponentsInChildren<KMHighlightable>(true).Where(x => x.gameObject.activeSelf).ToList();
                foreach (var highlightable in highlights)
                {
                    highlightable.gameObject.SetActive(false);
                }

                var instantiatedObject = InstantiateModuleCopy(selectedModule.GameObject);
                //Enable the highlights again because yes.
                foreach (var highlightable in highlights)
                {
                    highlightable.gameObject.SetActive(true);
                }
                
                //Move the module to the press
                yield return new WaitForSeconds(4f);
                MoveToPress();
                yield return new WaitForSeconds(3f);
                //Press the module
                ArmAnimator.SetTrigger(AnimationConstants.TrDown);
                StopCoroutine(_currentTextCoroutine);
                _currentTextCoroutine = StartCoroutine(HandleText(TextState.Constructing, selectedModule));

                yield return new WaitForSeconds(1f);
                //Replace the module with the new one.
                background.SetActive(false);
                SpawnPoint.SetActive(true);

                yield return new WaitForSeconds(1f);
                //Raise the press
                ArmAnimator.SetTrigger(AnimationConstants.TrUp);

                yield return new WaitForSeconds(2f);
                StopCoroutine(_currentTextCoroutine);
                _currentTextCoroutine = StartCoroutine(HandleText(TextState.Finished));
                //Move the module to the end.
                MoveToExit();
                
                yield return new WaitForSeconds(3f);
                //Remove it
                Destroy(instantiatedObject);
                SpawnPoint.SetActive(false);
            }
        }

        private GameObject InstantiateModuleCopy(GameObject module)
        {
            //Instantiate the object under a disabled parent, that way the awake function doesn't call.
            var instantiatedObject = Instantiate(module, SpawnPoint.transform.position, Quaternion.identity, SpawnPoint.transform);

            //Destroy all scripts on the module so it can't run any code
            var componentsToDestroy = instantiatedObject.GetComponentsInChildren<MonoBehaviour>(true).ToList();
            foreach (var component in componentsToDestroy)
            {
                Destroy(component);
            }

            instantiatedObject.transform.localScale = DefaultBackground.transform.localScale;
            return instantiatedObject;
        }
        
        private IEnumerator HandleText(TextState textState, KtaneModule module = null)
        {
            string activeString;
            switch (textState)
            {
                case TextState.Preparing when module != null:
                    activeString = $"preparing to assemble\n{module.Type} module:\n{module.Name}\nplease stand by.";
                    break;
                case TextState.Constructing when module != null:
                    activeString = $"assembling\n{module.Name}\nstand clear.";
                    break;
                case TextState.Finished:
                    activeString = "assembly finished\nshipping.";
                    break;
                default:
                    throw new InvalidOperationException("This should never happen, if this happened to you, we're in deep shit.");
            }
            
            //Little ... animation
            while (true)
            {
                DisplayText.text = activeString;
                yield return new WaitForSeconds(.3f);
                // ReSharper disable once Unity.InefficientPropertyAccess
                DisplayText.text = activeString + ".";
                yield return new WaitForSeconds(.3f);
                // ReSharper disable once Unity.InefficientPropertyAccess
                DisplayText.text = activeString + "..";
                yield return new WaitForSeconds(.3f);
            }
        }

        private void MoveToPress()
        {
            BeltAnimator.SetTrigger(AnimationConstants.TrSpin);
            BeltAnimator.SetTrigger(AnimationConstants.TrMoveToPress);
        }

        private void MoveToExit()
        {
            BeltAnimator.SetTrigger(AnimationConstants.TrSpin);
            BeltAnimator.SetTrigger(AnimationConstants.TrPressDone);
        }

        //All of this was written by Bashly, I stole it.
        private void OnLightChange(bool on)
        {
            if (_initialSwitchOn)
            {
                if (on)
                {
                    StartCoroutine(Setup());
                    StartCoroutine(ChangeLightIntensity(KMSoundOverride.SoundEffect.Switch, 0.0f, true));
                    _initialSwitchOn = false;
                }
                else
                {
                    StartCoroutine(ChangeLightIntensity(null, 0.0f, false));
                }
            }
            else
            {
                StartCoroutine(on
                    ? ChangeLightIntensity(KMSoundOverride.SoundEffect.LightBuzzShort, 0.5f, true)
                    : ChangeLightIntensity(KMSoundOverride.SoundEffect.LightBuzz, 1.5f, false));
            }
        }

        private IEnumerator ChangeLightIntensity(KMSoundOverride.SoundEffect? sound, float wait, bool lightState)
        {
            if (sound.HasValue)
            {
                Audio.PlayGameSoundAtTransform(sound.Value, transform);
            }

            if (wait > 0.0f)
            {
                yield return new WaitForSeconds(wait);
            }

            roomLight.enabled = lightState;
        }

        private void DebugLog(string s, params object[] p)
        {
            Debug.LogFormat($"[Module Assembly Station V2] {string.Format(s, p)}");
        }
    }
}