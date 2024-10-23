using TermRTS;

public class Renderer : IRenderer, IEventSink
{
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
        throw new NotImplementedException();
    }
    
    #endregion
    
    #region IRenderer Members
    
    public void RenderComponents(in IStorage storage, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
    }
    
    public void FinalizeRender()
    {
    }
    
    public void Shutdown()
    {
    }
    
    #endregion
}