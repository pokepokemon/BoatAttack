using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace BoatAttack.Benchmark
{
    public class Benchmark : MonoBehaviour
    {
        // data
        public BenchmarkData settings;
        private int benchIndex;
        private static BenchmarkSettings _settings;
        private static PerfomanceStats _stats;

        // Timing data
        public static int runNumber;
        public static int currentRunNumber;
        public static int runFrames;
        public static int currentRunFrames;
        private int totalRunFrames;
        private bool running = false;

        // Bench results
        private Dictionary<int, List<PerfBasic>> _perfData = new Dictionary<int, List<PerfBasic>>();
        public static List<PerfResults> PerfResults = new List<PerfResults>();

        //public AssetReference perfStatsUI;
        //public AssetReference perfSummaryUI;

        private void Start()
        {
            if (settings == null) AppSettings.ExitGame("Benchmark Not Setup");

            SceneManager.sceneLoaded += OnSceneLoaded;
            _stats = gameObject.AddComponent<PerfomanceStats>();
            DontDestroyOnLoad(gameObject);
            LoadBenchmark(settings.benchmarks[benchIndex]);
        }

        private void LoadBenchmark(BenchmarkSettings setting)
        {
            _perfData.Add(benchIndex, new List<PerfBasic>());
            _settings = setting;
            AppSettings.LoadScene(setting.scene);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != _settings.scene) return;

            if (_settings.warmup) currentRunNumber = -1;

            switch (_settings.type)
            {
                case BenchmarkType.Scene:
                    break;
                case BenchmarkType.Shader:
                    break;
                default:
                    AppSettings.ExitGame("Benchmark Not Setup");
                    break;
            }

            BeginRun();
            RenderPipelineManager.endFrameRendering += EndFrameRendering;
        }

        private void BeginRun()
        {
            currentRunFrames = 0;
            runFrames = _settings.runLength;

            _stats.enabled = _settings.stats;
            if(_settings.stats)
                _stats.StartRun(_settings.benchmarkName, _settings.runLength);
        }

        private void EndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            currentRunFrames++;
            if (currentRunFrames > _settings.runLength)
            {
                if (_settings.stats)
                {
                    var stats = _stats.EndRun();
                    if (stats != null)
                    {
                        _perfData[benchIndex].Add(stats);
                    }
                }

                currentRunNumber++;
                if (currentRunNumber < _settings.runs)
                {
                    BeginRun();
                }
                else
                {
                    RenderPipelineManager.endFrameRendering -= EndFrameRendering;
                    EndBenchmark();
                }
            }
        }

        public void EndBenchmark()
        {
            if(settings.saveData) SaveBenchmarkStats();
            benchIndex++;
            if (benchIndex < settings.benchmarks.Count)
            {
                LoadBenchmark(settings.benchmarks[benchIndex]);
            }
            else
            {
                switch (settings.finishAction)
                {
                    case FinishAction.Exit:
                        AppSettings.ExitGame();
                        break;
                    case FinishAction.ShowStats:
                        break;
                    case FinishAction.Nothing:
                        break;
                    default:
                        AppSettings.ExitGame("Benchmark Not Setup");
                        break;
                }
            }
        }

        private void SaveBenchmarkStats()
        {
            var path = GetResultPath() + $"/{_perfData[benchIndex][0].info.BenchmarkName}.txt";
            var data = new string[_perfData[benchIndex].Count];

            for (var index = 0; index < _perfData[benchIndex].Count; index++)
            {
                var perfData = _perfData[benchIndex][index];
                data[index] = JsonUtility.ToJson(perfData);
            }
            var results = new PerfResults();
            results.fileName = Path.GetFileName(path);
            results.filePath = Path.GetFullPath(path);
            results.timestamp = DateTime.Now;
            results.perfStats = _perfData[benchIndex].ToArray();
            PerfResults.Add(results);
            File.WriteAllLines(path, data);
        }

        public static string GetResultPath()
        {
            string path;
            if (Application.isEditor)
            {
                path = Directory.GetParent(Application.dataPath).ToString();
            }
            else
            {
                path = Application.persistentDataPath;
            }
            path += "/PerformanceResults";

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static List<PerfResults> LoadAllBenchmarkStats()
        {
            if (PerfResults.Count > 0)
            {
                return PerfResults;
            }
            else
            {
                var list = new List<PerfResults>();
                var fileList = Directory.GetFiles(GetResultPath());

                foreach (var file in fileList)
                {
                    if(!File.Exists(file))
                        break;

                    var result = new PerfResults();
                    var data = File.ReadAllLines(file);

                    if(data.Length == 0)
                        break;

                    //process data
                    result.fileName = Path.GetFileName(file);
                    result.filePath = Path.GetFullPath(file);
                    result.timestamp = File.GetCreationTime(file);
                    var perfData = data.Select(t => (PerfBasic) JsonUtility.FromJson(t, typeof(PerfBasic))).ToArray();
                    result.perfStats = perfData;
                    list.Add(result);
                }

                return list;
            }
        }
    }

#if UNITY_EDITOR
    public class BenchmarkTool
    {
        [MenuItem("Boat Attack/Benchmark/Island Flythrough")]
        public static void IslandFlyThrough()
        {

        }
    }
#endif

    public class PerfResults
    {
        public string fileName;
        public string filePath;
        public DateTime timestamp;
        public PerfBasic[] perfStats;
    }

    public class PerfBasic
    {
        public TestInfo info;
        public float RunTime;
        public int RunIndex;
        public int Frames;
        public float AvgMs;
        public float MinMs = Single.PositiveInfinity;
        public float MinMSFrame;
        public float MaxMs = Single.NegativeInfinity;
        public float MaxMSFrame;
        public float[] RawSamples;

        public PerfBasic(string benchmarkName, int frames)
        {
            Frames = frames;
            info = new TestInfo(benchmarkName);
        }
    }

    [Serializable]
    public class TestInfo
    {
        public string BenchmarkName;
        public string Scene;
        public string UnityVersion;
        public string UrpVersion;
        public string BoatAttackVersion;
        public string Platform;
        public string API;
        public string CPU;
        public string GPU;
        public string Os;
        public string Quality;
        public string Resolution;

        public TestInfo(string benchmarkName)
        {
            BenchmarkName = benchmarkName;
            Scene = Utility.RemoveWhitespace(SceneManager.GetActiveScene().name);
            UnityVersion = Application.unityVersion;
            UrpVersion = "N/A";
            BoatAttackVersion = Application.version;
            Platform =  Utility.RemoveWhitespace(Application.platform.ToString());
            API =  Utility.RemoveWhitespace(SystemInfo.graphicsDeviceType.ToString());
            CPU =  Utility.RemoveWhitespace(SystemInfo.processorType);
            GPU =  Utility.RemoveWhitespace(SystemInfo.graphicsDeviceName);
            Os =  Utility.RemoveWhitespace(SystemInfo.operatingSystem);
            Quality =  Utility.RemoveWhitespace(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Resolution = $"{Display.main.renderingWidth}x{Display.main.renderingHeight}";
        }
    }

    [Serializable]
    public enum BenchmarkType
    {
        Scene,
        Shader
    }

    [Serializable]
    public enum BenchmarkCameraType
    {
        Static,
        FlyThrough
    }

    [Serializable]
    public enum FinishAction
    {
        Exit,
        ShowStats,
        Nothing
    }

    [Serializable]
    public class BenchmarkSettings
    {
        public string benchmarkName;
        public string scene = "benchmark_island-flythrough";
        public BenchmarkType type;
        public int runs = 4;
        public int runLength = 1000;
        public bool warmup;
        public bool stats = false;
    }
}
