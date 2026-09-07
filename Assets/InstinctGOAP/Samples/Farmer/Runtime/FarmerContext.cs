using Instinct.GOAP;
using Instinct.GOAP.Unity;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{

    public sealed class FarmerContext : IMoveContext, ITickContext
    {
        public Transform Self;
        public Transform Home;
        public Transform Field;

        public int Energy = 100;
        public int CropsGrown;
        public int CropsRipe;

        public float Speed = 3f;
        public float ArriveDistance = 0.5f;

        public Vector3 Position => Self != null ? Self.position : Vector3.zero;
        public float DeltaTime => Time.deltaTime;

        public float DistanceTo(Transform target)
            => Self != null && target != null ? Vector3.Distance(Self.position, target.position) : 999f;

        public bool MoveTowards(Vector3 target)
        {
            if (Self == null) return true;

            Vector3 flat = new Vector3(target.x, Self.position.y, target.z);
            Vector3 delta = flat - Self.position;

            if (delta.sqrMagnitude <= ArriveDistance * ArriveDistance) return true;

            Self.position += delta.normalized * (Speed * Time.deltaTime);
            Self.forward = Vector3.Slerp(Self.forward, delta.normalized, 8f * Time.deltaTime);
            return false;
        }

        public override string ToString() =>
            $"E={Energy} ripe={CropsRipe} crops={CropsGrown} toHome={DistanceTo(Home):0.00} toField={DistanceTo(Field):0.00}";
    }
}
