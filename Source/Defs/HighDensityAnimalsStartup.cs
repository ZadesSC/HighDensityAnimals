using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public static class HighDensityAnimalsStartup
    {
        private static readonly FieldInfo SubWorkersIntField = typeof(PawnRenderNodeProperties).GetField("subWorkersInt", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            InjectDenseComps();
            InjectDenseAnimalRenderScale();
        }

        private static void InjectDenseComps()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race != null && def.race.Animal)
                {
                    EnsureComp(def, typeof(CompProperties_DenseAnimal), new CompProperties_DenseAnimal());
                }

                if (def.IsEgg)
                {
                    EnsureComp(def, typeof(CompProperties_DenseEgg), new CompProperties_DenseEgg());
                }
            }
        }

        private static void EnsureComp(ThingDef def, System.Type compType, CompProperties props)
        {
            if (def.comps == null)
            {
                def.comps = new List<CompProperties>();
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                if (compType.IsInstanceOfType(def.comps[i]))
                {
                    return;
                }
            }

            def.comps.Add(props);
        }

        private static void InjectDenseAnimalRenderScale()
        {
            PawnRenderTreeDef animalTree = DefDatabase<PawnRenderTreeDef>.GetNamedSilentFail("Animal");
            PawnRenderNodeProperties bodyNode = FindNode(animalTree?.root, PawnRenderNodeTagDefOf.Body);
            if (bodyNode == null)
            {
                return;
            }

            if (bodyNode.subworkerClasses == null)
            {
                bodyNode.subworkerClasses = new List<Type>();
            }

            Type scaleWorker = typeof(PawnRenderSubWorker_DenseAnimalScale);
            if (!bodyNode.subworkerClasses.Contains(scaleWorker))
            {
                bodyNode.subworkerClasses.Add(scaleWorker);
                SubWorkersIntField?.SetValue(bodyNode, null);
            }
        }

        private static PawnRenderNodeProperties FindNode(PawnRenderNodeProperties node, PawnRenderNodeTagDef tagDef)
        {
            if (node == null)
            {
                return null;
            }

            if (node.tagDef == tagDef)
            {
                return node;
            }

            if (node.children == null)
            {
                return null;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                PawnRenderNodeProperties match = FindNode(node.children[i], tagDef);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
