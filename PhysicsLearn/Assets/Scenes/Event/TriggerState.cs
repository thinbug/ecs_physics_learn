
using Unity.Entities;


[GenerateAuthoringComponent]
public struct TriggerState : IComponentData
{
    public int enter_frame;
    public int stay_frame;
}