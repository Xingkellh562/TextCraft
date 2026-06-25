using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Core.EntityModule;

namespace TextCraft.src.Rendering.EntityRender
{
    internal class ModelRenderMgr
    {
        public void Update(World world)
        {
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(Transform), typeof(ModelCompenent) }))
            {
                var model = world.ecsMgr.GetComponent<ModelCompenent>(entity);
                var trans = world.ecsMgr.GetComponent<Transform>(entity);
                //world.gridMgr.grids["Entity"][model.posIndex].Pos += new OpenTK.Mathematics.Vector3(0,0,0.005f);
                //Console.WriteLine(world.gridMgr.grids["Entity"][model.posIndex].Pos);
                world.gridMgr.grids["Entity"][model.posIndex].Pos = trans.position;

            }
        }
    }
}
