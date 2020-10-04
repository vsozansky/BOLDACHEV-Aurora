using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AuroraCore.Storage;

namespace AuroraCore.Controllers {
    public abstract class Controller {
        protected IStorageAPI Storage { get; private set; }

        public static T Instantiate<T>(IStorageAPI storage) where T : Controller, new() {
            T value = new T();
            value.Storage = storage;
            return value;
        }

        public IEnumerable<ReactionBase> GetReactions() {
            var methods = this.GetType().GetMethods();
            foreach (var method in methods) {
                var attributes = method.GetCustomAttributes(typeof(EventReactionAttribute));
                if (attributes.Count() == 0) {
                    continue;
                }

                EventReactionAttribute reaction = (EventReactionAttribute)attributes.Single();
                yield return new ReactionBase(reaction.EventID, method);
            }

            yield break;
        }

        protected bool TryGetCondition<T>(IEventData e, out T value) where T : ConditionRule {
            if (e.Conditions is T) {
                value = e.Conditions as T;
                return true;
            }
            else {
                value = null;
                return false;
            }
        }

        protected IEnumerable<ConditionRule> TraverseConditions(ConditionRule stack) {
            if (stack is ConditionRule.ComplexConditionRule complex) {
                foreach (var rule in complex.Values) {
                    foreach (var subRule in TraverseConditions(rule)) {
                        yield return subRule;
                    }
                }
            }
            else {
                yield return stack;
            }
        }
    }
}