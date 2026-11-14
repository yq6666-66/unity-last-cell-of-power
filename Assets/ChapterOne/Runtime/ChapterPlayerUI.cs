using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.ChapterTacticsPolish;

namespace LastCellOfPower.ChapterOne
{
    public enum ChapterViewMode { Loading, Intro, Playing, Paused, Settings, Help, Results }

    /// <summary>Player-facing presentation only. All gameplay changes use Session commands.</summary>
    public sealed class ChapterPlayerUI : MonoBehaviour
    {
        public ExpandedSession session;
        public EmergencyPulseController pulse;
        public RunStatistics statistics;
        public TMP_FontAsset font;
        public ChapterAudioFeedback audioFeedback;
        public ChapterArtProfile artProfile;
        public TacticalReadabilityProfile polishProfile;
        public PolishFeedbackPresenter polishFeedback;
        public ChapterViewMode Mode { get; private set; } = ChapterViewMode.Loading;
        public float MasterVolume { get; private set; }
        public float SensitivityMultiplier { get; private set; }
        public bool ModalVisible => Mode != ChapterViewMode.Playing;
        public bool LayoutBuilt => built;
        public int VisibleThreatCount { get; private set; }
        public string DisplayedPulseTargetId { get; private set; } = "";
        public int HelpRuleCardCount {get;private set;}
        public bool PolishFeedbackActive=>polishProfile && polishFeedback && polishFeedback.IsAvailable;
        public string VisibleFeedbackText=>toastText && toastText.gameObject.activeInHierarchy?toastText.text:"";
        public string VisibleTutorialText=>tutorialText && tutorialText.gameObject.activeInHierarchy?tutorialText.text:"";
        public string VisibleInteractionText=>promptText && promptText.gameObject.activeInHierarchy?promptText.text:"";
        static bool introShown, chargeTaught, terminalTaught, alertTaught;
        const string VolumeKey = "LastCell.ChapterOne.MasterVolume";
        const string SensitivityKey = "LastCell.ChapterOne.Sensitivity";
        Color panelColor => artProfile ? artProfile.panelColor : new Color(.025f, .055f, .07f, .94f);
        Color mint => artProfile ? artProfile.accentColor : new Color(.40f, .93f, .74f, 1);
        RectTransform canvasRect, hudRoot, modalRoot;
        CanvasGroup modalFade;
        TextMeshProUGUI energyText, zoneText, objectiveText, promptText, pulseText, threatText, toastText, tutorialText;
        Image energyFill, warningFill;
        Sprite fillSprite;
        GameObject threatPanel, crosshair;
        GameObject secondThreatPanel;
        TextMeshProUGUI secondThreatText;
        Image secondWarningFill;
        readonly List<ExpandedEnemy> subscribedGuards=new List<ExpandedEnemy>();
        float toastUntil, tutorialUntil, resultAge, initialSensitivity = .12f, previousEnergy;
        bool built, initialized, subscribed,polishWasAvailable;
        RunSummary result;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetApplicationHints() { introShown = chargeTaught = terminalTaught = alertTaught = false; }

        void Start()
        {
            if (!session || !pulse || !statistics || !font)
            { Debug.LogError("CHAPTER_UI: required references or bundled font missing"); enabled = false; return; }
            session.externalPlayerUI = true;
            initialSensitivity = session.player.sensitivity;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .7f));
            SensitivityMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1), .5f, 2);
            ApplySettings(); Build(); Subscribe();
        }
        void OnEnable() { if (built) Subscribe(); }
        void OnDisable() { Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); if(fillSprite)Destroy(fillSprite); }
        void Subscribe()
        {
            if (subscribed || !session || !pulse) return;
            session.PauseChanged += OnPause; session.RunEnded += OnRunEnded;
            foreach(var guard in session.Guards)
                if(guard && !subscribedGuards.Contains(guard)){guard.StateChanged+=OnGuardState;subscribedGuards.Add(guard);}
            pulse.Resolved += OnPulse;
            subscribed = true;
        }
        void Unsubscribe()
        {
            if (!subscribed) return;
            if (session) { session.PauseChanged -= OnPause; session.RunEnded -= OnRunEnded; }
            foreach(var guard in subscribedGuards)if(guard)guard.StateChanged-=OnGuardState;
            subscribedGuards.Clear();
            if (pulse) pulse.Resolved -= OnPulse;
            subscribed = false;
        }
        void Update()
        {
            if (!built || !session) return;
            if (!initialized && (session.Ready || session.Outcome != RunOutcome.Running))
            {
                initialized = true; previousEnergy = session.battery.Current;
                if (session.Outcome != RunOutcome.Running) OnRunEnded(session.Summary);
                else SetMode(introShown ? ChapterViewMode.Paused : ChapterViewMode.Intro);
            }
            if (!initialized) return;
            if (session.Outcome != RunOutcome.Running && Mode != ChapterViewMode.Results) OnRunEnded(session.Summary);
            if (Mode == ChapterViewMode.Playing && session.Paused) SetMode(ChapterViewMode.Paused);
            if (Mode == ChapterViewMode.Results)
            {
                resultAge += Time.unscaledDeltaTime;
                modalFade.alpha = Mathf.Clamp01(resultAge / .65f);
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) SkipResultFade();
            }
            if ((Mode == ChapterViewMode.Settings || Mode == ChapterViewMode.Help) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ReturnToPause();
            UpdateHud();
        }
        void UpdateHud()
        {
            float energy = session.battery.Current;
            energyText.text = $"电量  {energy:0} / {session.rules.maximumEnergy:0}";
            energyFill.fillAmount = Mathf.Clamp01(energy / session.rules.maximumEnergy);
            energyFill.color = energy < 20 ? new Color(1,.48f,.28f) : mint;
            zoneText.text = "最后一格电  /  " + session.CurrentZone;
            objectiveText.text = session.interaction.Powered ? "出口已供电 · 前往东北区北门" :
                !chargeTaught ? "建议先补电，再前往设备区终端" : "目标：设备区终端供电 · 预留撤离电量";
            if (energy < 20 && session.Outcome == RunOutcome.Running) objectiveText.text += "\n低电量：请留意附近充电座";
            if(artProfile)
            {
                // The bundled CJK face needs 26.1 reference pixels per 18 pt
                // line. Low-energy copy can occupy three lines after wrapping.
                bool low=energy<20 && session.Outcome==RunOutcome.Running;
                objectiveText.rectTransform.sizeDelta=new Vector2(312,low?82:56);
                ((RectTransform)objectiveText.transform.parent).sizeDelta=new Vector2(336,low?98:72);
            }
            if (session.interaction.Charging && energy > previousEnergy + .001f && !chargeTaught)
            { chargeTaught = true; Teach("充电时可以转头观察。移动或松开 E 即可离开。", 6); }
            previousEnergy = energy;
            if (session.interaction.Candidate && session.interaction.Candidate.kind == InteractionKind.Terminal && !terminalTaught)
            { terminalTaught = true; Teach($"终端需要 {session.rules.supplyStartMinimum:0} 电量开始；完成扣 {session.rules.supplyCost:0}，还要留电撤离。", 6); }
            promptText.text = session.interaction.Charging ? $"充电中  {energy:0.0} / {session.rules.maximumEnergy:0}\n移动或松开 E 结束" :
                session.interaction.Supplying ? $"供电中  {session.interaction.SupplyElapsed:0.0} / {session.rules.supplySeconds:0.#} 秒\n移动可取消；未完成不扣供电费用" : session.interaction.Hint;
            if(session.interaction.UsingEnvironment && session.interaction.EnvironmentDuration>0)
                promptText.text=$"{session.interaction.Current.label}  {session.interaction.EnvironmentElapsed:0.0} / {session.interaction.EnvironmentDuration:0.#} 秒\n移动或松开 E 可取消";
            if(PolishFeedbackActive && !string.IsNullOrEmpty(polishFeedback.CurrentHint))promptText.text=polishFeedback.CurrentHint;
            promptText.transform.parent.gameObject.SetActive(Mode == ChapterViewMode.Playing && !string.IsNullOrEmpty(promptText.text));
            bool pulseReady=pulse.ReadyToUse;
            var selected=pulseReady?pulse.SelectedTarget:null;
            DisplayedPulseTargetId=DualGuardMode && selected?GuardId(selected):"";
            pulseText.text = pulse.CooldownRemaining > 0 ? $"Q  应急干扰   {pulse.CooldownRemaining:0.0}s" :
                energy < pulse.profile.minimumEnergy ? $"Q  应急干扰   至少 {pulse.profile.minimumEnergy:0} 电量" :
                pulseReady ? (DualGuardMode?$"Q  干扰 {DisplayedPulseTargetId}   耗电 {pulse.profile.cost:0}":$"Q  可干扰威胁   消耗 {pulse.profile.cost:0} 电量") : $"Q  应急干扰   消耗 {pulse.profile.cost:0} 电量";
            pulseText.color = pulseReady ? mint : new Color(.78f,.84f,.86f);
            UpdateThreats();
            bool polish=PolishFeedbackActive;
            if(polish)
            {
                toastUntil=tutorialUntil=0;toastText.text=polishFeedback.CurrentFeedbackText;
                tutorialText.text=polishFeedback.CurrentTutorialText;
                tutorialText.gameObject.SetActive(Mode==ChapterViewMode.Playing && polishFeedback.TutorialVisible);
            }
            else
            {
                if(polishWasAvailable && Time.unscaledTime>=toastUntil)toastText.text="";
                if(polishWasAvailable && Time.unscaledTime>=tutorialUntil)tutorialText.text="";
                toastText.text = Time.unscaledTime < toastUntil ? toastText.text : "";
                tutorialText.gameObject.SetActive(Mode == ChapterViewMode.Playing && Time.unscaledTime < tutorialUntil);
            }
            polishWasAvailable=polish;
            crosshair.SetActive(Mode == ChapterViewMode.Playing);
        }
        bool DualGuardMode=>session && session.Guards.Count>1;
        string GuardId(ExpandedEnemy guard)
        {
            if(!guard)return "";
            if(!string.IsNullOrWhiteSpace(guard.guardId))return guard.guardId;
            for(int i=0;i<session.Guards.Count;i++)if(session.Guards[i]==guard)return (i+1).ToString("00");
            return "--";
        }
        void UpdateThreats()
        {
            VisibleThreatCount=0;
            if(DualGuardMode)
            {
                int row=0;
                UpdateThreatRow(session.Guards[0],threatPanel,threatText,warningFill,ref row);
                UpdateThreatRow(session.Guards[1],secondThreatPanel,secondThreatText,secondWarningFill,ref row);
                VisibleThreatCount=row;return;
            }
            var state = session.enemy.State;
            bool threat = state == ExpandedEnemyState.Alert || state == ExpandedEnemyState.Chase || state == ExpandedEnemyState.Disrupted;
            threatPanel.SetActive(threat && Mode == ChapterViewMode.Playing);
            if(secondThreatPanel)secondThreatPanel.SetActive(false);
            VisibleThreatCount=threatPanel.activeSelf?1:0;
            if (threat)
            {
                if (state == ExpandedEnemyState.Disrupted)
                    threatText.text = $"干扰生效  {session.enemy.DisruptionRemaining:0.0}s\n现在离开视线，别停在原地";
                else
                    threatText.text = (state == ExpandedEnemyState.Alert ? "正在被发现" : "正在被追击") + " · " + ThreatDirection() + "\n利用墙角遮挡，或在有效范围内按 Q";
                warningFill.fillAmount = state == ExpandedEnemyState.Disrupted ? Mathf.Clamp01(session.enemy.DisruptionRemaining / pulse.profile.disruptionSeconds) : session.enemy.Awareness01;
                warningFill.color = state == ExpandedEnemyState.Disrupted ? mint : new Color(1,.62f,.28f);
            }
        }
        void UpdateThreatRow(ExpandedEnemy guard,GameObject panel,TextMeshProUGUI text,Image fill,ref int row)
        {
            if(!panel)return;
            bool visible=Mode==ChapterViewMode.Playing && guard && (guard.State==ExpandedEnemyState.Alert || guard.State==ExpandedEnemyState.Chase);
            panel.SetActive(visible);if(!visible)return;
            ((RectTransform)panel.transform).anchoredPosition=new Vector2(-16,-16-row*(artProfile?100:104));row++;
            bool chase=guard.State==ExpandedEnemyState.Chase;
            text.text=$"巡逻机 {GuardId(guard)} · {(chase?"追击":"警觉")} · {ThreatDirection(guard)}\n{(chase?"利用遮挡脱离视线":"正在被发现，尽快遮断视线")}";
            fill.fillAmount=guard.Awareness01;
            fill.color=chase?new Color(1,.32f,.20f):new Color(1,.65f,.24f);
        }
        string ThreatDirection(ExpandedEnemy guard=null)
        {
            if(!guard)guard=session.enemy;
            Vector3 local = session.player.transform.InverseTransformDirection(guard.transform.position - session.player.transform.position);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(angle) < 35 ? "前方" : Mathf.Abs(angle) > 145 ? "后方" : angle > 0 ? "右侧" : "左侧";
        }
        void OnGuardState(ExpandedEnemyState previous, ExpandedEnemyState next)
        {
            if (!alertTaught && (next == ExpandedEnemyState.Alert || next == ExpandedEnemyState.Chase))
            { alertTaught = true; Teach("可以绕到掩体后，或按 Q 干扰附近威胁。成功干扰消耗 12 电量。", 6); }
        }
        void Teach(string text,float duration)
        {
            if(!built)return;
            if(PolishFeedbackActive){polishFeedback.RequestTutorial(text,duration);return;}
            tutorialText.text=text;tutorialUntil=Time.unscaledTime+duration;
        }
        void OnPulse(PulseUseResult value)
        {
            if (!built) return;
            string message = value == PulseUseResult.Success ? "干扰命中 · 争取时间撤离" :
                value == PulseUseResult.CoolingDown ? "干扰正在冷却" :
                value == PulseUseResult.InsufficientEnergy ? $"干扰需要至少 {pulse.profile.minimumEnergy:0} 电量" :
                value == PulseUseResult.NoValidTarget ? "没有有效威胁：需在 4 米内且无遮挡" :
                value == PulseUseResult.WaitingForRelease ? "先松开 Q，再重新按下" : "";
            if(value==PulseUseResult.Success && DualGuardMode && pulse.LastResolvedTarget)
                message=$"已干扰巡逻机 {GuardId(pulse.LastResolvedTarget)} · 留意另一台机器";
            if (message.Length == 0) return;
            if(PolishFeedbackActive){polishFeedback.ShowPulseFeedback(value,message);return;}
            toastText.text=message;toastUntil=Time.unscaledTime+2.2f;
        }
        void OnPause(bool paused)
        {
            if (!built || !initialized || session.Outcome != RunOutcome.Running) return;
            if (!paused) SetMode(ChapterViewMode.Playing);
            else if (Mode == ChapterViewMode.Playing) SetMode(ChapterViewMode.Paused);
        }
        void OnRunEnded(RunSummary summary)
        {
            if (!built) return;
            result=summary;SetMode(ChapterViewMode.Results);resultAge=0;modalFade.alpha=.15f;
        }
        public void BeginOrContinue()
        {
            if (!session || !session.Ready || session.Outcome != RunOutcome.Running) return;
            PlayClick();session.Resume();
            if (!session.Paused) { introShown=true;SetMode(ChapterViewMode.Playing); }
        }
        public void ShowSettings() { if (!session || session.Outcome != RunOutcome.Running) return; session.Pause();PlayClick();SetMode(ChapterViewMode.Settings); }
        public void ShowHelp() { if (!session || session.Outcome != RunOutcome.Running) return;session.Pause();PlayClick();SetMode(ChapterViewMode.Help); }
        public void ReturnToPause() { PlayClick();SetMode(ChapterViewMode.Paused); }
        public void RestartRun() { if (!session || session.Outcome == RunOutcome.Running) return;PlayClick();introShown=true;session.Restart(); }
        public void SkipResultFade() { resultAge=1;if(modalFade)modalFade.alpha=1; }
        public void SetMasterVolume(float value) { MasterVolume=Mathf.Clamp01(value);AudioListener.volume=MasterVolume;PlayerPrefs.SetFloat(VolumeKey,MasterVolume); }
        public void SetSensitivity(float value) { SensitivityMultiplier=Mathf.Clamp(value,.5f,2);if(session&&session.player)session.player.sensitivity=initialSensitivity*SensitivityMultiplier;PlayerPrefs.SetFloat(SensitivityKey,SensitivityMultiplier); }
        public void RestoreDefaults() { SetMasterVolume(.7f);SetSensitivity(1);PlayClick();if(Mode==ChapterViewMode.Settings)SetMode(ChapterViewMode.Settings); }
        void ApplySettings() { AudioListener.volume=MasterVolume;session.player.sensitivity=initialSensitivity*SensitivityMultiplier; }
        void PlayClick() { if(audioFeedback)audioFeedback.PlayUiClick(); }

        void Build()
        {
            var canvasGo=new GameObject("ChapterPlayerCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            canvasRect=canvasGo.GetComponent<RectTransform>();
            if(!FindFirstObjectByType<EventSystem>())
            { var input=new GameObject("ChapterEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));input.transform.SetParent(transform,false);input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            hudRoot=Rect("HUD",canvasRect,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var status=Panel("Status",hudRoot,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-16),new Vector2(350,92),new Vector2(0,1));
            zoneText=Text(status,"Zone",new Vector2(14,-10),new Vector2(322,26),18,mint);
            energyText=Text(status,"Energy",new Vector2(14,-39),new Vector2(322,30),25,Color.white);
            var energyBack=Panel("EnergyTrack",status,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-78),new Vector2(322,5),new Vector2(0,1),new Color(.15f,.22f,.25f));
            energyFill=Fill(energyBack,mint);
            var goal=Panel("Objective",hudRoot,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-116),new Vector2(350,68),new Vector2(0,1));
            objectiveText=Text(goal,"Goal",new Vector2(14,-10),new Vector2(322,50),20,Color.white);
            var ability=Panel("Pulse",hudRoot,Vector2.zero,Vector2.zero,new Vector2(16,16),new Vector2(350,48),Vector2.zero);
            pulseText=Text(ability,"PulseText",new Vector2(14,-10),new Vector2(322,30),20,Color.white);
            var threat=Panel("Threat",hudRoot,Vector2.one,Vector2.one,new Vector2(-16,-16),new Vector2(360,92),new Vector2(1,1));threatPanel=threat.gameObject;
            threatText=Text(threat,"ThreatText",new Vector2(14,-10),new Vector2(332,64),20,Color.white);
            var threatBack=Panel("ThreatTrack",threat,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-80),new Vector2(332,5),new Vector2(0,1),new Color(.15f,.22f,.25f));warningFill=Fill(threatBack,mint);
            if(DualGuardMode)
            {
                var other=Panel("SecondThreat",hudRoot,Vector2.one,Vector2.one,new Vector2(-16,-120),new Vector2(360,92),new Vector2(1,1));secondThreatPanel=other.gameObject;
                secondThreatText=Text(other,"SecondThreatText",new Vector2(14,-10),new Vector2(332,64),20,Color.white);
                var otherTrack=Panel("SecondThreatTrack",other,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-80),new Vector2(332,5),new Vector2(0,1),new Color(.15f,.22f,.25f));secondWarningFill=Fill(otherTrack,mint);
                secondThreatPanel.SetActive(false);
            }
            var prompt=Panel("Interaction",hudRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,78),new Vector2(560,62),new Vector2(.5f,0));
            promptText=Text(prompt,"Prompt",new Vector2(14,-8),new Vector2(532,48),21,Color.white);promptText.alignment=TextAlignmentOptions.Center;
            toastText=Text(hudRoot,"Toast",Vector2.zero,new Vector2(560,32),20,mint);Anchor(toastText.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,148));
            tutorialText=Text(hudRoot,"Hint",Vector2.zero,new Vector2(600,62),20,new Color(.94f,.86f,.65f));Anchor(tutorialText.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,190));tutorialText.alignment=TextAlignmentOptions.Center;
            var reticle=Text(hudRoot,"Reticle",Vector2.zero,new Vector2(24,24),25,Color.white);Anchor(reticle.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero);reticle.text="+";reticle.alignment=TextAlignmentOptions.Center;crosshair=reticle.gameObject;
            modalRoot=Rect("Modal",canvasRect,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);modalFade=modalRoot.gameObject.AddComponent<CanvasGroup>();
            if(artProfile)ApplyArtLayout();
            built=true;SetMode(ChapterViewMode.Loading);
        }
        void ApplyArtLayout()
        {
            // Theme only: keep names, hierarchy and input callbacks used by the
            // accepted chapter. Reference sizes remain readable at 960 x 540.
            // Noto CJK line-height / point-size = 104.256 / 72 = 1.448.
            // Reserve the line box, not just visible Latin capital height.
            var status=(RectTransform)hudRoot.Find("Status");status.sizeDelta=new Vector2(316,100);
            zoneText.rectTransform.anchoredPosition=new Vector2(12,-8);zoneText.rectTransform.sizeDelta=new Vector2(292,28);zoneText.fontSize=17;
            energyText.rectTransform.anchoredPosition=new Vector2(12,-38);energyText.rectTransform.sizeDelta=new Vector2(292,38);energyText.fontSize=24;
            var energyTrack=(RectTransform)status.Find("EnergyTrack");energyTrack.anchoredPosition=new Vector2(12,-86);energyTrack.sizeDelta=new Vector2(292,4);
            energyTrack.GetComponent<Image>().color=new Color(.16f,.19f,.19f);
            var objective=(RectTransform)hudRoot.Find("Objective");objective.anchoredPosition=new Vector2(16,-124);objective.sizeDelta=new Vector2(336,72);
            objectiveText.rectTransform.anchoredPosition=new Vector2(12,-8);objectiveText.rectTransform.sizeDelta=new Vector2(312,56);objectiveText.fontSize=18;
            var ability=(RectTransform)hudRoot.Find("Pulse");ability.sizeDelta=new Vector2(316,44);
            pulseText.rectTransform.anchoredPosition=new Vector2(12,-8);pulseText.rectTransform.sizeDelta=new Vector2(292,28);pulseText.fontSize=18;
            var threat=(RectTransform)hudRoot.Find("Threat");threat.sizeDelta=new Vector2(340,88);
            threatText.rectTransform.anchoredPosition=new Vector2(12,-8);threatText.rectTransform.sizeDelta=new Vector2(316,62);threatText.fontSize=18;
            var threatTrack=(RectTransform)threat.Find("ThreatTrack");threatTrack.anchoredPosition=new Vector2(12,-77);threatTrack.sizeDelta=new Vector2(316,4);
            threatTrack.GetComponent<Image>().color=new Color(.18f,.17f,.15f);
            if(secondThreatPanel)
            {
                var other=(RectTransform)secondThreatPanel.transform;other.sizeDelta=new Vector2(340,88);
                secondThreatText.rectTransform.anchoredPosition=new Vector2(12,-8);secondThreatText.rectTransform.sizeDelta=new Vector2(316,62);secondThreatText.fontSize=18;
                var track=(RectTransform)other.Find("SecondThreatTrack");track.anchoredPosition=new Vector2(12,-77);track.sizeDelta=new Vector2(316,4);track.GetComponent<Image>().color=new Color(.18f,.17f,.15f);
            }
            var interaction=(RectTransform)hudRoot.Find("Interaction");interaction.anchoredPosition=new Vector2(0,74);interaction.sizeDelta=new Vector2(560,80);
            promptText.rectTransform.sizeDelta=new Vector2(532,64);
            promptText.fontSize=20;toastText.fontSize=19;tutorialText.fontSize=19;
            toastText.rectTransform.anchoredPosition=new Vector2(0,166);toastText.rectTransform.sizeDelta=new Vector2(560,60);
            tutorialText.rectTransform.anchoredPosition=new Vector2(0,238);tutorialText.rectTransform.sizeDelta=new Vector2(600,64);
            var reticle=crosshair.GetComponent<TextMeshProUGUI>();reticle.fontSize=19;reticle.rectTransform.sizeDelta=new Vector2(30,32);reticle.color=new Color(.88f,.92f,.88f,.85f);
            AddRule(status,new Vector2(0,1),new Vector2(1,1),new Color(mint.r,mint.g,mint.b,.55f));
            AddRule(ability,new Vector2(0,0),new Vector2(1,0),new Color(mint.r,mint.g,mint.b,.24f));
        }
        void AddRule(Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var r=Rect("ThemeRule",parent,min,max,Vector2.zero,new Vector2(0,1));
            var graphic=r.gameObject.AddComponent<Image>();graphic.color=color;graphic.raycastTarget=false;
        }
        void SetMode(ChapterViewMode mode)
        {
            Mode=mode;modalFade.alpha=1;HelpRuleCardCount=0;
            hudRoot.gameObject.SetActive(mode==ChapterViewMode.Playing || mode==ChapterViewMode.Paused && !polishProfile);
            modalRoot.gameObject.SetActive(mode!=ChapterViewMode.Playing);
            foreach(Transform child in modalRoot) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
            if(mode==ChapterViewMode.Playing)return;
            var shade=Panel("Shade",modalRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero,artProfile?new Color(.008f,.010f,.012f,.60f):new Color(.005f,.015f,.02f,.74f));
            bool polishHelp=mode==ChapterViewMode.Help && polishProfile;
            Color modalCardColor=panelColor;
            // Modal reading surfaces are opaque in the actual product, so bright
            // nearby world lettering cannot compete with menu copy. The world
            // remains visible around the card; no world objects are hidden.
            if(polishProfile)modalCardColor.a=1;
            var card=Panel("Card",shade,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,polishHelp?600:490),new Vector2(.5f,.5f),modalCardColor);
            if(artProfile)AddRule(card,new Vector2(0,1),new Vector2(1,1),new Color(mint.r,mint.g,mint.b,.65f));
            Text(card,"Kicker",new Vector2(30,-24),new Vector2(560,24),16,mint).text="LAST CELL OF POWER  /  CHAPTER ONE";
            string title=mode==ChapterViewMode.Intro?"最后一格电":mode==ChapterViewMode.Settings?"设置":mode==ChapterViewMode.Help?"操作说明":mode==ChapterViewMode.Results?ResultTitle():mode==ChapterViewMode.Loading?"正在准备仓库":"已暂停";
            Text(card,"Title",new Vector2(30,-60),new Vector2(560,artProfile?58:52),36,Color.white).text=title;
            if(polishHelp)
            {
                RuleCard(card,"HelpRuleQ",130,polishProfile.helpQTitle,string.Format(polishProfile.helpQBody,pulse.profile.range,pulse.profile.minimumEnergy,pulse.profile.cost));
                RuleCard(card,"HelpRuleNoise",246,polishProfile.helpNoiseTitle,string.Format(polishProfile.helpNoiseBody,session.tactics.profile.noiseSignalSeconds));
                RuleCard(card,"HelpRuleShutter",362,polishProfile.helpShutterTitle,string.Format(polishProfile.helpShutterBody,session.tactics.profile.shutterClosedSeconds));
                Text(card,"HelpControls",new Vector2(30,-476),new Vector2(560,26),16,new Color(.80f,.85f,.82f)).text=polishProfile.helpControls;
                Button(card,"返回暂停菜单",-560,ReturnToPause);return;
            }
            if(mode==ChapterViewMode.Intro || mode==ChapterViewMode.Help)
            {
                Text(card,"Body",new Vector2(30,-126),new Vector2(560,226),session.tactics?20:22,new Color(.87f,.91f,.92f)).text=session.tactics?
                    "两台巡逻机仍在运行。为出口供电，带着余电离开。\n先补电，再到设备区终端，最后去东北区北门。\nWASD 移动 · 鼠标观察 · 按住 E 充电或供电\nQ 干扰最近有效目标：需 13 电量，命中耗 12。\n诱导器与隔断门：按 E 启用，留意使用限制。\n移动或松开 E 可中断交互；Esc 暂停。":
                    "仓库已封锁。你需要为出口供电，带着余电离开。\n\n先补电，再到设备区终端，最后去东北区北门。\nWASD 移动 · 鼠标观察 · 按住 E 操作\nQ 干扰：需 13 电量，命中耗 12，无需瞄准。\n移动或松开 E 可中断交互；Esc 暂停。";
                if(mode==ChapterViewMode.Intro)
                { Button(card,"开始探索",-380,BeginOrContinue);Button(card,"跳过说明并开始",-434,BeginOrContinue,false); }
                else Button(card,"返回暂停菜单",-420,ReturnToPause);
            }
            else if(mode==ChapterViewMode.Paused)
            {
                Text(card,"Body",new Vector2(30,-126),new Vector2(560,artProfile?70:62),23,new Color(.87f,.91f,.92f)).text="电量、敌人和干扰计时均已暂停。\n继续前请松开移动键、E 和 Q。";
                Button(card,"继续游戏",artProfile?-250:-228,BeginOrContinue);Button(card,"设置",artProfile?-310:-288,ShowSettings,false);Button(card,"操作说明",artProfile?-370:-348,ShowHelp,false);
            }
            else if(mode==ChapterViewMode.Settings)
            {
                Text(card,"VolumeLabel",new Vector2(30,artProfile?-124:-130),new Vector2(560,artProfile?36:28),23,Color.white).text="主音量";
                Slider(card,"Volume",-182,0,1,MasterVolume,SetMasterVolume);
                Text(card,"SensitivityLabel",new Vector2(30,artProfile?-222:-228),new Vector2(560,artProfile?36:28),23,Color.white).text="鼠标灵敏度  /  0.5 至 2 倍";
                Slider(card,"Sensitivity",-280,.5f,2,SensitivityMultiplier,SetSensitivity);
                Button(card,"恢复默认",-360,RestoreDefaults,false);Button(card,"返回暂停菜单",-420,ReturnToPause);
            }
            else if(mode==ChapterViewMode.Results)
            {
                var summary=result??session.Summary;
                string body=summary==null?session.Failure:$"本局用时  {Mathf.FloorToInt(summary.ActiveSeconds/60):00}:{Mathf.FloorToInt(summary.ActiveSeconds%60):00}\n剩余电量  {summary.RemainingEnergy:0.0}\n成功干扰  {summary.SuccessfulPulseCount} 次\n\n"+ResultExplanation(summary);
                if(summary!=null && session.tactics)
                    body=$"本局用时  {Mathf.FloorToInt(summary.ActiveSeconds/60):00}:{Mathf.FloorToInt(summary.ActiveSeconds%60):00}\n剩余电量  {summary.RemainingEnergy:0.0}  ·  成功干扰  {summary.SuccessfulPulseCount} 次\n诱导装置使用  {summary.UsedNoiseCount} 次\n隔断门  {(summary.ShutterUsed?"已使用":"未使用")}\n\n"+ResultExplanation(summary);
                Text(card,"Summary",new Vector2(30,-130),new Vector2(560,artProfile?220:205),session.tactics?21:24,new Color(.87f,.91f,.92f)).text=body;
                Button(card,"再来一局  /  R",artProfile?-408:-370,RestartRun);Button(card,"显示完整结果  /  Space",artProfile?-466:-430,SkipResultFade,false);
            }
            else Text(card,"Loading",new Vector2(30,-160),new Vector2(560,100),24,Color.white).text="正在初始化本局，请稍候。";
        }
        void RuleCard(RectTransform parent,string name,float top,string heading,string body)
        {
            var card=Panel(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-top),new Vector2(560,106),new Vector2(0,1),new Color(.055f,.072f,.071f,.98f));
            Text(card,"RuleTitle",new Vector2(12,-6),new Vector2(536,28),18,mint).text=heading;
            Text(card,"RuleBody",new Vector2(12,-36),new Vector2(536,62),17,new Color(.89f,.92f,.90f)).text=body;HelpRuleCardCount++;
        }
        string ResultTitle() { return session.Outcome==RunOutcome.Escaped?"逃生成功":session.Outcome==RunOutcome.Caught?"被抓住了":session.Outcome==RunOutcome.Depleted?"电量耗尽":"本局已停止"; }
        string ResultExplanation(RunSummary summary)
        {
            if(summary.Outcome==RunOutcome.Escaped)return "你带着最后的电量离开了封锁区。";
            if(summary.Outcome==RunOutcome.Depleted)return summary.WasPowered?"出口已经供电，但撤离途中电量耗尽。":"到达出口前电量耗尽，下次可以提前补电。";
            if(summary.Outcome==RunOutcome.Caught)return "巡逻机完成了捕获确认。可以利用遮挡，或为应急干扰预留电量。";
            return session.Failure;
        }
        RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 position,Vector2 size,Vector2 pivot,Color? color=null)
        {var r=Rect(name,parent,min,max,position,size);r.pivot=pivot;var image=r.gameObject.AddComponent<Image>();image.color=color??panelColor;return r;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static void Anchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 pivot,Vector2 position){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.anchoredPosition=position;}
        TextMeshProUGUI Text(Transform parent,string name,Vector2 position,Vector2 size,float fontSize,Color color)
        {var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),position,size);r.pivot=new Vector2(0,1);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=fontSize;text.color=color;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;}
        Image Fill(Transform parent,Color color)
        {
            // Image generates Simple geometry when no sprite is assigned, even for
            // Type.Filled. Own the Sprite only; Texture2D.whiteTexture is shared.
            if(!fillSprite)
            {
                fillSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
                fillSprite.name="ChapterFillSprite";fillSprite.hideFlags=HideFlags.DontSave;
            }
            var r=Rect("Fill",parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var image=r.gameObject.AddComponent<Image>();image.sprite=fillSprite;image.color=color;
            image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;
            image.raycastTarget=false;return image;
        }
        void Button(Transform parent,string label,float y,UnityEngine.Events.UnityAction onClick,bool primary=true)
        {
            Color buttonColor=artProfile?(primary?new Color(.16f,.30f,.25f):new Color(.105f,.125f,.127f)):(primary?new Color(.16f,.48f,.39f):new Color(.10f,.18f,.22f));
            var r=Panel(label,parent,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,y),new Vector2(560,46),new Vector2(.5f,0),buttonColor);
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=r.GetComponent<Image>();button.onClick.AddListener(onClick);
            var text=Text(r,"Label",new Vector2(12,-6),new Vector2(536,artProfile?36:34),23,Color.white);text.alignment=TextAlignmentOptions.Center;
            text.text=label;
        }
        void Slider(Transform parent,string name,float y,float min,float max,float value,UnityEngine.Events.UnityAction<float> callback)
        {
            var r=Rect(name,parent,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,y),new Vector2(530,34));r.pivot=new Vector2(.5f,.5f);
            var track=Panel("Track",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f),artProfile?new Color(.15f,.19f,.19f):new Color(.14f,.24f,.28f));track.offsetMin=new Vector2(0,12);track.offsetMax=new Vector2(0,-12);
            var fillArea=Rect("FillArea",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fillArea.offsetMin=new Vector2(10,12);fillArea.offsetMax=new Vector2(-10,-12);
            var fill=Panel("Fill",fillArea,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f),mint);
            var handleArea=Rect("HandleArea",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);handleArea.offsetMin=new Vector2(10,0);handleArea.offsetMax=new Vector2(-10,0);
            var handle=Panel("Handle",handleArea,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(20,0),new Vector2(.5f,.5f),Color.white);
            var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.SetValueWithoutNotify(value);slider.onValueChanged.AddListener(callback);
        }
    }
}
