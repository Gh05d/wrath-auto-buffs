using BuffIt2TheLimit.Handlers;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuffIt2TheLimit {
    public class AnimatedExecutionEngine : IBuffExecutionEngine {
        private UnitCommand Cast(CastTask task) {
            try {
                if (task.SourceType != BuffSourceType.Spell) {
                    // Equipment/scroll/potion: use instant cast.
                    // UnitUseAbility.CreateCastCommand rejects synthetic AbilityData
                    // that isn't in the caster's actual ability list.
                    EventBus.Subscribe(new EngineCastingHandler(task, true));
                    Rulebook.Trigger<RuleCastSpell>(new(task.SpellToCast, task.Target));
                    return null;
                }

                // Spells: use animated command (real AbilityData from spellbook)
                EventBus.Subscribe(new EngineCastingHandler(task));
                return UnitUseAbility.CreateCastCommand(task.SpellToCast, task.Target);
            }
            catch (Exception ex) {
                Main.Error(ex, "Animated Engine Casting");
                return null;
            }
        }

        public IEnumerator CreateSpellCastRoutine(List<CastTask> tasks) {
            var byCaster = tasks.GroupBy(task => task.Caster).Select(x => x.GetEnumerator()).ToList();
            UnitCommand[] running = new UnitCommand[byCaster.Count];

            int remaining = byCaster.Count;

            while (byCaster.Any(x => x != null)) {

                for (int i = 0; i < byCaster.Count; i++) {
                    var current = running[i];
                    if (current != null) {
                        if (current.IsFinished) {
                            running[i] = null;
                        }
                        continue;
                    }


                    var queue = byCaster[i];
                    if (queue == null) {
                        continue;
                    }

                    if (!queue.MoveNext()) {
                        byCaster[i] = null;
                        continue;
                    }

                    current = Cast(queue.Current);
                    if (current != null) {
                        queue.Current.Caster.Commands.Run(current);
                        running[i] = current;
                    }
                    break;
                }

                yield return new WaitForFixedUpdate();

            }
            yield return null;
        }
    }
}
