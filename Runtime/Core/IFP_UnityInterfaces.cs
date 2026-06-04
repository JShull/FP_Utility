// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
