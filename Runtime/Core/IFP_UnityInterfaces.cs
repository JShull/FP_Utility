namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Interface for forcing Start
    /// </summary>
    public interface IFPOnStartSetup
    {
        public bool SetupStart { get; set; }
        public void Start();
    }
    /// <summary>
    /// Interface for forcing Awake
    /// </summary>
    public interface IFPOnAwakeSetup
    {
        public bool SetupAwake { get; set; }
        public void Awake();
    }
    /// <summary>
    /// Interface for setting up a dont destroy scenario
    /// </summary>
    public interface IFPDontDestroy
    {
        public bool DontDestroy { get; set; }
        public void Awake();
    }
    /// <summary>
    /// Interface for forcing OnEnable/Disable
    /// Setup delegate to have others listen in
    /// </summary>
    public interface IFPOnEnableDisable
    {
        public bool UseOnEnable { get; set; }
        public bool UseOnDisable { get;set; }
        public void OnEnable();
        public void OnDisable();
        
    }
    public interface IFPAfterSceneLoadBootstrap
    {
        public void InitializeAfterSceneLoad();
    }
}
