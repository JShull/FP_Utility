namespace FuzzPhyte.Utility
{
    using UnityEngine;
    public interface IFPBinder
    {
        void Bind();
        void Unbind();
        void ResetBind();
        bool IsBound { get; }
    }
    public interface IFPBindableEndpoint { } // mainly a tag
    public interface IFPEventSource<T>
    {
        event System.Action<T> OnEvent;
    }
    public interface IFPCommandTarget<T>
    {
        void Execute(T cmd);        
    }
    public abstract class FP_BinderBase : MonoBehaviour,IFPBinder
    {
        public bool IsBound { get; private set; }

        protected virtual void OnEnable() => Bind();
        protected virtual void OnDisable() => Unbind();
        protected virtual void Reset()=>ResetBind();

        public void Bind()
        {
            if (IsBound) return;
            IsBound = true;
            OnBind();
        }

        public void Unbind()
        {
            if (!IsBound) return;
            IsBound = false;
            OnUnbind();
        }
        public void ResetBind()
        {
            IsBound = false;
            OnResetBind();
        }

        protected abstract void OnBind();
        protected abstract void OnUnbind();
        protected abstract void OnResetBind();
    }
}
