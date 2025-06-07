using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Utils.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public class ModAnalyzer
    {
        private readonly ILogManager _logManager;
        private readonly Dictionary<string, TextFrameworkSignature> _frameworkSignatures;
        private readonly Dictionary<string, ConflictPattern> _knownConflictPatterns;
        
        public ModAnalyzer(ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _frameworkSignatures = InitializeFrameworkSignatures();
            _knownConflictPatterns = InitializeConflictPatterns();
        }
        
        public async Task<ModAnalysisResult> AnalyzeModAsync(ModInfo modInfo)
        {
            if (modInfo == null)
                throw new ArgumentNullException(nameof(modInfo));
                
            var result = new ModAnalysisResult
            {
                ModId = modInfo.Id,
                ModName = modInfo.Name,
                AnalysisStartTime = DateTime.Now
            };
            
            try
            {
                _logManager.LogInfo($"모드 분석 시작: {modInfo.Name}");
                
                await AnalyzeAssemblyAsync(modInfo, result);
                await DetectTextFrameworksAsync(modInfo, result);
                await GenerateTranslationPatternsAsync(modInfo, result);
                await DetectConflictsAsync(modInfo, result);
                await AnalyzePerformanceImpactAsync(modInfo, result);
                
                result.AnalysisEndTime = DateTime.Now;
                result.IsSuccess = true;
                
                _logManager.LogInfo($"모드 분석 완료: {modInfo.Name} - 소요시간: {result.AnalysisDuration.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                _logManager.LogError($"모드 분석 실패: {modInfo.Name}", ex);
            }
            
            return result;
        }
        
        public ModTranslationPattern GenerateTranslationPattern(ModInfo modInfo, ModAnalysisResult analysisResult)
        {
            var pattern = new ModTranslationPattern
            {
                ModId = modInfo.Id,
                PatternName = $"Auto-generated pattern for {modInfo.Name}",
                Description = $"자동 생성된 번역 패턴 - {modInfo.Name} v{modInfo.Version}",
                Priority = 100,
                Version = "1.0.0"
            };
            
            // 감지된 텍스트 프레임워크를 기반으로 패턴 생성
            foreach (var framework in analysisResult.DetectedTextFrameworks)
            {
                pattern.TextFrameworkPatterns.Add(new TextFrameworkPattern
                {
                    FrameworkName = framework.Name,
                    MethodPattern = framework.MethodPattern,
                    PropertyPattern = framework.PropertyPattern,
                    Priority = framework.Priority
                });
            }
            
            // 감지된 문자열 패턴을 기반으로 키/값 변환 규칙 생성
            foreach (var stringPattern in analysisResult.DetectedStringPatterns)
            {
                if (stringPattern.IsKey)
                {
                    pattern.KeyPatterns.Add(new KeyPattern
                    {
                        Pattern = stringPattern.Pattern,
                        Replacement = stringPattern.Replacement,
                        IsRegex = stringPattern.IsRegex
                    });
                }
                else
                {
                    pattern.ValuePatterns.Add(new ValuePattern
                    {
                        Pattern = stringPattern.Pattern,
                        Replacement = stringPattern.Replacement,
                        IsRegex = stringPattern.IsRegex
                    });
                }
            }
            
            // 컨텍스트 패턴 추가
            foreach (var context in analysisResult.DetectedContexts)
            {
                pattern.ContextPatterns.Add(new ContextPattern
                {
                    Pattern = context.Pattern,
                    IsRegex = context.IsRegex
                });
            }
            
            return pattern;
        }
        
        public List<string> DetectPotentialConflicts(ModInfo targetMod, List<ModInfo> installedMods)
        {
            var conflicts = new List<string>();
            
            try
            {
                foreach (var installedMod in installedMods.Where(m => m.Id != targetMod.Id))
                {
                    var conflictLevel = AnalyzeModConflict(targetMod, installedMod);
                    
                    if (conflictLevel > ConflictLevel.None)
                    {
                        conflicts.Add($"모드 {installedMod.Name}와 {conflictLevel} 수준의 충돌 가능성");
                    }
                }
                
                // 알려진 충돌 패턴 확인
                foreach (var conflictPattern in _knownConflictPatterns.Values)
                {
                    if (conflictPattern.Matches(targetMod))
                    {
                        conflicts.Add($"알려진 충돌 패턴 감지: {conflictPattern.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"충돌 감지 중 오류: {targetMod.Name}", ex);
            }
            
            return conflicts;
        }
        
        private async Task AnalyzeAssemblyAsync(ModInfo modInfo, ModAnalysisResult result)
        {
            if (string.IsNullOrEmpty(modInfo.AssemblyPath) || !File.Exists(modInfo.AssemblyPath))
                return;
                
            await Task.Run(() =>
            {
                try
                {
                    using var assembly = AssemblyDefinition.ReadAssembly(modInfo.AssemblyPath);
                    
                    result.AssemblyInfo = new AssemblyAnalysisInfo
                    {
                        FullName = assembly.FullName,
                        TargetFramework = assembly.MainModule.RuntimeVersion,
                        ModuleCount = assembly.Modules.Count,
                        TypeCount = assembly.Modules.Sum(m => m.Types.Count),
                        MethodCount = assembly.Modules.SelectMany(m => m.Types).SelectMany(t => t.Methods).Count()
                    };
                    
                    // BepInEx 플러그인 정보 추출
                    var bepInExAttribute = assembly.CustomAttributes
                        .FirstOrDefault(attr => attr.AttributeType.Name == "BepInPluginAttribute");
                    
                    if (bepInExAttribute != null)
                    {
                        result.AssemblyInfo.IsBepInExPlugin = true;
                        result.AssemblyInfo.PluginGuid = bepInExAttribute.ConstructorArguments[0].Value?.ToString();
                        result.AssemblyInfo.PluginName = bepInExAttribute.ConstructorArguments[1].Value?.ToString();
                        result.AssemblyInfo.PluginVersion = bepInExAttribute.ConstructorArguments[2].Value?.ToString();
                    }
                    
                    // 의존성 분석
                    result.AssemblyInfo.Dependencies = assembly.MainModule.AssemblyReferences
                        .Select(ar => ar.Name)
                        .ToList();
                    
                    // Harmony 패치 사용 여부 확인
                    result.AssemblyInfo.UsesHarmony = result.AssemblyInfo.Dependencies
                        .Any(dep => dep.Contains("Harmony") || dep.Contains("0Harmony"));
                }
                catch (Exception ex)
                {
                    _logManager.LogError($"어셈블리 분석 실패: {modInfo.AssemblyPath}", ex);
                }
            });
        }
        
        private async Task DetectTextFrameworksAsync(ModInfo modInfo, ModAnalysisResult result)
        {
            if (string.IsNullOrEmpty(modInfo.AssemblyPath) || !File.Exists(modInfo.AssemblyPath))
                return;
                
            await Task.Run(() =>
            {
                try
                {
                    using var assembly = AssemblyDefinition.ReadAssembly(modInfo.AssemblyPath);
                    
                    foreach (var module in assembly.Modules)
                    {
                        foreach (var type in module.Types)
                        {
                            foreach (var method in type.Methods)
                            {
                                if (method.HasBody)
                                {
                                    AnalyzeMethodForTextFrameworks(method, result);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogError($"텍스트 프레임워크 분석 실패: {modInfo.AssemblyPath}", ex);
                }
            });
        }
        
        private void AnalyzeMethodForTextFrameworks(MethodDefinition method, ModAnalysisResult result)
        {
            try
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    {
                        var methodRef = instruction.Operand as MethodReference;
                        if (methodRef != null)
                        {
                            var fullName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";
                            
                            foreach (var signature in _frameworkSignatures.Values)
                            {
                                if (signature.Matches(fullName))
                                {
                                    var existing = result.DetectedTextFrameworks
                                        .FirstOrDefault(f => f.Name == signature.FrameworkName);
                                    
                                    if (existing == null)
                                    {
                                        result.DetectedTextFrameworks.Add(new DetectedTextFramework
                                        {
                                            Name = signature.FrameworkName,
                                            MethodPattern = signature.MethodPattern,
                                            PropertyPattern = signature.PropertyPattern,
                                            Priority = signature.Priority,
                                            UsageCount = 1,
                                            DetectedMethods = new List<string> { fullName }
                                        });
                                    }
                                    else
                                    {
                                        existing.UsageCount++;
                                        if (!existing.DetectedMethods.Contains(fullName))
                                        {
                                            existing.DetectedMethods.Add(fullName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogDebug($"메서드 분석 중 오류: {method.FullName}", ex);
            }
        }
        
        private async Task GenerateTranslationPatternsAsync(ModInfo modInfo, ModAnalysisResult result)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 문자열 리터럴 분석
                    AnalyzeStringLiterals(modInfo, result);
                    
                    // 리소스 파일 분석
                    AnalyzeResourceFiles(modInfo, result);
                    
                    // JSON/XML 설정 파일 분석
                    AnalyzeConfigurationFiles(modInfo, result);
                }
                catch (Exception ex)
                {
                    _logManager.LogError($"번역 패턴 생성 실패: {modInfo.Name}", ex);
                }
            });
        }
        
        private void AnalyzeStringLiterals(ModInfo modInfo, ModAnalysisResult result)
        {
            if (string.IsNullOrEmpty(modInfo.AssemblyPath) || !File.Exists(modInfo.AssemblyPath))
                return;
                
            try
            {
                using var assembly = AssemblyDefinition.ReadAssembly(modInfo.AssemblyPath);
                var stringLiterals = new HashSet<string>();
                
                foreach (var module in assembly.Modules)
                {
                    foreach (var type in module.Types)
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.HasBody)
                            {
                                foreach (var instruction in method.Body.Instructions)
                                {
                                    if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string str)
                                    {
                                        if (IsTranslatableString(str))
                                        {
                                            stringLiterals.Add(str);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 문자열 패턴 분석 및 생성
                foreach (var literal in stringLiterals)
                {
                    var patterns = AnalyzeStringPattern(literal);
                    result.DetectedStringPatterns.AddRange(patterns);
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"문자열 리터럴 분석 실패: {modInfo.AssemblyPath}", ex);
            }
        }
        
        private void AnalyzeResourceFiles(ModInfo modInfo, ModAnalysisResult result)
        {
            if (string.IsNullOrEmpty(modInfo.InstallPath) || !Directory.Exists(modInfo.InstallPath))
                return;
                
            try
            {
                var resourceFiles = Directory.GetFiles(modInfo.InstallPath, "*.resx", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(modInfo.InstallPath, "*.resources", SearchOption.AllDirectories));
                
                foreach (var resourceFile in resourceFiles)
                {
                    result.DetectedContexts.Add(new DetectedContext
                    {
                        Pattern = Path.GetFileNameWithoutExtension(resourceFile),
                        IsRegex = false,
                        ContextType = "Resource",
                        SourceFile = resourceFile
                    });
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"리소스 파일 분석 실패: {modInfo.InstallPath}", ex);
            }
        }
        
        private void AnalyzeConfigurationFiles(ModInfo modInfo, ModAnalysisResult result)
        {
            if (string.IsNullOrEmpty(modInfo.InstallPath) || !Directory.Exists(modInfo.InstallPath))
                return;
                
            try
            {
                var configFiles = Directory.GetFiles(modInfo.InstallPath, "*.json", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(modInfo.InstallPath, "*.xml", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(modInfo.InstallPath, "*.config", SearchOption.AllDirectories));
                
                foreach (var configFile in configFiles)
                {
                    var fileName = Path.GetFileName(configFile).ToLowerInvariant();
                    
                    if (fileName.Contains("language") || fileName.Contains("locale") || fileName.Contains("translation"))
                    {
                        result.DetectedContexts.Add(new DetectedContext
                        {
                            Pattern = Path.GetFileNameWithoutExtension(configFile),
                            IsRegex = false,
                            ContextType = "Localization",
                            SourceFile = configFile
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"설정 파일 분석 실패: {modInfo.InstallPath}", ex);
            }
        }
        
        private async Task DetectConflictsAsync(ModInfo modInfo, ModAnalysisResult result)
        {
            await Task.Run(() =>
            {
                try
                {
                    result.PotentialConflicts = DetectPotentialConflicts(modInfo, new List<ModInfo>());
                    
                    // Harmony 패치 충돌 분석
                    if (result.AssemblyInfo?.UsesHarmony == true)
                    {
                        AnalyzeHarmonyConflicts(modInfo, result);
                    }
                    
                    // 텍스트 프레임워크 충돌 분석
                    AnalyzeTextFrameworkConflicts(modInfo, result);
                }
                catch (Exception ex)
                {
                    _logManager.LogError($"충돌 감지 실패: {modInfo.Name}", ex);
                }
            });
        }
        
        private void AnalyzeHarmonyConflicts(ModInfo modInfo, ModAnalysisResult result)
        {
            // Harmony 패치 대상 메서드 분석
            if (string.IsNullOrEmpty(modInfo.AssemblyPath) || !File.Exists(modInfo.AssemblyPath))
                return;
                
            try
            {
                using var assembly = AssemblyDefinition.ReadAssembly(modInfo.AssemblyPath);
                
                foreach (var module in assembly.Modules)
                {
                    foreach (var type in module.Types)
                    {
                        var harmonyPatchAttribute = type.CustomAttributes
                            .FirstOrDefault(attr => attr.AttributeType.Name.Contains("HarmonyPatch"));
                            
                        if (harmonyPatchAttribute != null)
                        {
                            var targetClass = harmonyPatchAttribute.ConstructorArguments
                                .FirstOrDefault(arg => arg.Type.Name == "Type")?.Value?.ToString();
                            var targetMethod = harmonyPatchAttribute.ConstructorArguments
                                .FirstOrDefault(arg => arg.Type.Name == "String")?.Value?.ToString();
                                
                            if (!string.IsNullOrEmpty(targetClass) && !string.IsNullOrEmpty(targetMethod))
                            {
                                result.HarmonyPatches.Add(new DetectedHarmonyPatch
                                {
                                    TargetClass = targetClass,
                                    TargetMethod = targetMethod,
                                    PatchType = GetHarmonyPatchType(type),
                                    PatchClass = type.FullName
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"Harmony 충돌 분석 실패: {modInfo.AssemblyPath}", ex);
            }
        }
        
        private void AnalyzeTextFrameworkConflicts(ModInfo modInfo, ModAnalysisResult result)
        {
            var conflictingFrameworks = new[]
            {
                "UnityEngine.UI.Text",
                "TMPro.TextMeshProUGUI",
                "UnityEngine.GUIText"
            };
            
            var detectedFrameworkNames = result.DetectedTextFrameworks.Select(f => f.Name).ToList();
            var conflicts = detectedFrameworkNames.Where(f => conflictingFrameworks.Contains(f)).ToList();
            
            if (conflicts.Count > 1)
            {
                result.PotentialConflicts.Add($"여러 텍스트 프레임워크 동시 사용으로 인한 충돌 가능성: {string.Join(", ", conflicts)}");
            }
        }
        
        private async Task AnalyzePerformanceImpactAsync(ModInfo modInfo, ModAnalysisResult result)
        {
            await Task.Run(() =>
            {
                try
                {
                    result.PerformanceImpact = new PerformanceImpactAnalysis
                    {
                        EstimatedMemoryUsage = EstimateMemoryUsage(modInfo, result),
                        EstimatedCpuImpact = EstimateCpuImpact(modInfo, result),
                        EstimatedStartupTime = EstimateStartupTime(modInfo, result),
                        TextProcessingOverhead = EstimateTextProcessingOverhead(result)
                    };
                }
                catch (Exception ex)
                {
                    _logManager.LogError($"성능 영향 분석 실패: {modInfo.Name}", ex);
                }
            });
        }
        
        private bool IsTranslatableString(string str)
        {
            if (string.IsNullOrWhiteSpace(str) || str.Length < 3)
                return false;
                
            // GUID, 경로, URL 등 번역하지 않을 문자열 패턴 제외
            var excludePatterns = new[]
            {
                @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$", // GUID
                @"^[a-zA-Z]:\\.*", // Windows 경로
                @"^/.*", // Unix 경로
                @"^https?://.*", // URL
                @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", // 이메일
                @"^\d+$", // 숫자만
                @"^[A-Z_]+$", // 상수명
                @"^[a-z]+([A-Z][a-z]*)*$" // camelCase (길이가 짧은 경우)
            };
            
            foreach (var pattern in excludePatterns)
            {
                if (Regex.IsMatch(str, pattern))
                    return false;
            }
            
            // 한글, 영어 문장 패턴 확인
            return Regex.IsMatch(str, @"[가-힣]|\b[A-Z][a-z]+\s+[a-z]|\s");
        }
        
        private List<DetectedStringPattern> AnalyzeStringPattern(string literal)
        {
            var patterns = new List<DetectedStringPattern>();
            
            // 키-값 쌍 패턴 감지
            if (literal.Contains(':') || literal.Contains('='))
            {
                patterns.Add(new DetectedStringPattern
                {
                    Pattern = literal,
                    IsKey = true,
                    IsRegex = false,
                    Confidence = 0.8f
                });
            }
            
            // 포맷 문자열 패턴 감지
            if (literal.Contains("{0}") || literal.Contains("%s") || literal.Contains("%d"))
            {
                patterns.Add(new DetectedStringPattern
                {
                    Pattern = literal,
                    IsKey = false,
                    IsRegex = false,
                    Confidence = 0.9f
                });
            }
            
            return patterns;
        }
        
        private ConflictLevel AnalyzeModConflict(ModInfo mod1, ModInfo mod2)
        {
            var conflictLevel = ConflictLevel.None;
            
            // 같은 어셈블리 이름 확인
            if (Path.GetFileName(mod1.AssemblyPath) == Path.GetFileName(mod2.AssemblyPath))
            {
                conflictLevel = ConflictLevel.High;
            }
            
            // 같은 GUID 확인 (BepInEx 플러그인의 경우)
            if (mod1.Id == mod2.Id && !string.IsNullOrEmpty(mod1.Id))
            {
                conflictLevel = ConflictLevel.Critical;
            }
            
            return conflictLevel;
        }
        
        private string GetHarmonyPatchType(TypeDefinition type)
        {
            if (type.Methods.Any(m => m.Name == "Prefix"))
                return "Prefix";
            if (type.Methods.Any(m => m.Name == "Postfix"))
                return "Postfix";
            if (type.Methods.Any(m => m.Name == "Transpiler"))
                return "Transpiler";
            if (type.Methods.Any(m => m.Name == "Finalizer"))
                return "Finalizer";
                
            return "Unknown";
        }
        
        private long EstimateMemoryUsage(ModInfo modInfo, ModAnalysisResult result)
        {
            long estimatedMemory = 1024 * 1024; // 기본 1MB
            
            if (result.AssemblyInfo != null)
            {
                estimatedMemory += result.AssemblyInfo.TypeCount * 100;
                estimatedMemory += result.AssemblyInfo.MethodCount * 50;
            }
            
            estimatedMemory += result.DetectedStringPatterns.Count * 200;
            estimatedMemory += result.DetectedTextFrameworks.Count * 10240;
            
            return estimatedMemory;
        }
        
        private float EstimateCpuImpact(ModInfo modInfo, ModAnalysisResult result)
        {
            float impact = 0.1f; // 기본 영향도
            
            impact += result.HarmonyPatches.Count * 0.05f;
            impact += result.DetectedTextFrameworks.Count * 0.02f;
            
            return Math.Min(impact, 1.0f);
        }
        
        private int EstimateStartupTime(ModInfo modInfo, ModAnalysisResult result)
        {
            int startupTime = 100; // 기본 100ms
            
            if (result.AssemblyInfo != null)
            {
                startupTime += result.AssemblyInfo.TypeCount / 10;
                startupTime += result.AssemblyInfo.MethodCount / 100;
            }
            
            startupTime += result.DetectedStringPatterns.Count;
            startupTime += result.HarmonyPatches.Count * 10;
            
            return startupTime;
        }
        
        private float EstimateTextProcessingOverhead(ModAnalysisResult result)
        {
            float overhead = 0.01f; // 기본 1%
            
            overhead += result.DetectedTextFrameworks.Count * 0.005f;
            overhead += result.DetectedStringPatterns.Count * 0.001f;
            
            return Math.Min(overhead, 0.1f); // 최대 10%
        }
        
        private Dictionary<string, TextFrameworkSignature> InitializeFrameworkSignatures()
        {
            return new Dictionary<string, TextFrameworkSignature>
            {
                ["UnityUI"] = new TextFrameworkSignature
                {
                    FrameworkName = "UnityEngine.UI.Text",
                    MethodPattern = @"UnityEngine\.UI\.Text\.set_text",
                    PropertyPattern = @"UnityEngine\.UI\.Text\.text",
                    Priority = 100,
                    MatchPatterns = new[] { "UnityEngine.UI.Text" }
                },
                ["TextMeshPro"] = new TextFrameworkSignature
                {
                    FrameworkName = "TMPro.TextMeshProUGUI",
                    MethodPattern = @"TMPro\.TextMeshProUGUI\.set_text",
                    PropertyPattern = @"TMPro\.TextMeshProUGUI\.text",
                    Priority = 90,
                    MatchPatterns = new[] { "TMPro.TextMeshProUGUI", "TMPro.TextMeshPro" }
                },
                ["LegacyGUI"] = new TextFrameworkSignature
                {
                    FrameworkName = "UnityEngine.GUIText",
                    MethodPattern = @"UnityEngine\.GUIText\.set_text",
                    PropertyPattern = @"UnityEngine\.GUIText\.text",
                    Priority = 80,
                    MatchPatterns = new[] { "UnityEngine.GUIText" }
                },
                ["IMGUI"] = new TextFrameworkSignature
                {
                    FrameworkName = "UnityEngine.GUI",
                    MethodPattern = @"UnityEngine\.GUI\.(Label|Button|TextField)",
                    PropertyPattern = @"UnityEngine\.GUI\..*",
                    Priority = 70,
                    MatchPatterns = new[] { "UnityEngine.GUI.Label", "UnityEngine.GUI.Button" }
                }
            };
        }
        
        private Dictionary<string, ConflictPattern> InitializeConflictPatterns()
        {
            return new Dictionary<string, ConflictPattern>
            {
                ["CommonLocalization"] = new ConflictPattern
                {
                    Name = "CommonLocalization",
                    Description = "일반적인 로컬라이제이션 플러그인 충돌",
                    ConflictPatterns = new[] { "Localization", "Translation", "Language" },
                    ConflictLevel = ConflictLevel.Medium
                },
                ["HarmonyTextPatching"] = new ConflictPattern
                {
                    Name = "HarmonyTextPatching",
                    Description = "Harmony 텍스트 패치 충돌",
                    ConflictPatterns = new[] { "UnityEngine.UI.Text.set_text", "TMPro.TextMeshProUGUI.set_text" },
                    ConflictLevel = ConflictLevel.High
                }
            };
        }
    }

    // 지원 클래스들
    public class ModAnalysisResult
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public DateTime AnalysisStartTime { get; set; }
        public DateTime AnalysisEndTime { get; set; }
        public TimeSpan AnalysisDuration => AnalysisEndTime - AnalysisStartTime;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        
        public AssemblyAnalysisInfo AssemblyInfo { get; set; }
        public List<DetectedTextFramework> DetectedTextFrameworks { get; set; }
        public List<DetectedStringPattern> DetectedStringPatterns { get; set; }
        public List<DetectedContext> DetectedContexts { get; set; }
        public List<DetectedHarmonyPatch> HarmonyPatches { get; set; }
        public List<string> PotentialConflicts { get; set; }
        public PerformanceImpactAnalysis PerformanceImpact { get; set; }
        
        public ModAnalysisResult()
        {
            DetectedTextFrameworks = new List<DetectedTextFramework>();
            DetectedStringPatterns = new List<DetectedStringPattern>();
            DetectedContexts = new List<DetectedContext>();
            HarmonyPatches = new List<DetectedHarmonyPatch>();
            PotentialConflicts = new List<string>();
        }
    }

    public class AssemblyAnalysisInfo
    {
        public string FullName { get; set; }
        public string TargetFramework { get; set; }
        public int ModuleCount { get; set; }
        public int TypeCount { get; set; }
        public int MethodCount { get; set; }
        public bool IsBepInExPlugin { get; set; }
        public string PluginGuid { get; set; }
        public string PluginName { get; set; }
        public string PluginVersion { get; set; }
        public List<string> Dependencies { get; set; }
        public bool UsesHarmony { get; set; }
        
        public AssemblyAnalysisInfo()
        {
            Dependencies = new List<string>();
        }
    }

    public class DetectedTextFramework
    {
        public string Name { get; set; }
        public string MethodPattern { get; set; }
        public string PropertyPattern { get; set; }
        public int Priority { get; set; }
        public int UsageCount { get; set; }
        public List<string> DetectedMethods { get; set; }
        
        public DetectedTextFramework()
        {
            DetectedMethods = new List<string>();
        }
    }

    public class DetectedStringPattern
    {
        public string Pattern { get; set; }
        public string Replacement { get; set; }
        public bool IsKey { get; set; }
        public bool IsRegex { get; set; }
        public float Confidence { get; set; }
    }

    public class DetectedContext
    {
        public string Pattern { get; set; }
        public bool IsRegex { get; set; }
        public string ContextType { get; set; }
        public string SourceFile { get; set; }
    }

    public class DetectedHarmonyPatch
    {
        public string TargetClass { get; set; }
        public string TargetMethod { get; set; }
        public string PatchType { get; set; }
        public string PatchClass { get; set; }
    }

    public class PerformanceImpactAnalysis
    {
        public long EstimatedMemoryUsage { get; set; }
        public float EstimatedCpuImpact { get; set; }
        public int EstimatedStartupTime { get; set; }
        public float TextProcessingOverhead { get; set; }
    }

    public class TextFrameworkSignature
    {
        public string FrameworkName { get; set; }
        public string MethodPattern { get; set; }
        public string PropertyPattern { get; set; }
        public int Priority { get; set; }
        public string[] MatchPatterns { get; set; }
        
        public bool Matches(string methodName)
        {
            return MatchPatterns?.Any(pattern => methodName.Contains(pattern)) == true;
        }
    }

    public class ConflictPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] ConflictPatterns { get; set; }
        public ConflictLevel ConflictLevel { get; set; }
        
        public bool Matches(ModInfo modInfo)
        {
            var modName = modInfo.Name?.ToLowerInvariant() ?? "";
            var assemblyName = Path.GetFileNameWithoutExtension(modInfo.AssemblyPath)?.ToLowerInvariant() ?? "";
            
            return ConflictPatterns?.Any(pattern => 
                modName.Contains(pattern.ToLowerInvariant()) || 
                assemblyName.Contains(pattern.ToLowerInvariant())) == true;
        }
    }

    public enum ConflictLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}