using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuroraCore.Types;

namespace AuroraCore.Storage.Implementation {
    public class MemoryStorage : IStorageAdapter {
        private Dictionary<int, IEvent> events = new Dictionary<int, IEvent>();
        private IDataContext context;

        public MemoryStorage(ITypeManager typeManager) {
            context = new DataContext(this, typeManager);
        }

        public async Task AddEvent(IEvent e) {
            await Task.Yield();
            events.Add(e.ID, e);
        }

        public async Task<IEvent> GetEvent(int id) {
            await Task.Yield();
            if (events.TryGetValue(id, out var value)) {
                return value;
            }
            else {
                return null;
            }
        }

        public async Task<IEnumerable<IEvent>> GetEvents(int offset = 0, int limit = 10) {
            await Task.Yield();
            return events.Skip(offset).Take(limit).Select(e => e.Value);
        }

        public async Task<IAttr> GetAttribute(int id) {
            await Task.Yield();

            var attrDef = await GetEvent(id);

            if (attrDef.BaseEventID != StaticEvent.Attribute) {
                return null;
            }

            return new Attr(context, attrDef);
        }

        public async Task<IEnumerable<IAttr>> GetAttributes() {
            await Task.Yield();

            var attrEvents =
                from e in events
                where e.Value.BaseEventID == StaticEvent.Attribute && e.Value.ValueID == StaticEvent.Individual
                select e.Value;

            return attrEvents.Select(a => new Attr(context, a));
        }

        public async Task<IAttrModel> GetAttrModel() {
            await Task.Yield();

            var modelDef = (
                from e in events
                where e.Value.BaseEventID == StaticEvent.Attribute && e.Value.ValueID == StaticEvent.Model
                select e.Value
            ).SingleOrDefault();

            if (null == modelDef) {
                return null;
            }

            return new AttrModel(context, modelDef);
        }

        public async Task<IAttrProperty> GetAttrProperty(int propertyID) {
            await Task.Yield();

            var propertyDef = (
                from e in events
                where
                    e.Value.BaseEventID == StaticEvent.AttributeProperty &&
                    e.Value.ValueID == StaticEvent.SubEvent &&
                    e.Key == propertyID
                select e.Value
            ).SingleOrDefault();

            if (null == propertyDef) {
                return null;
            }

            return new AttrProperty(context, propertyDef);
        }

        public async Task<IEnumerable<IAttrProperty>> GetAttrProperties() {
            await Task.Yield();

            var propertyIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.AttributeProperty
                select Int32.Parse(e.Value.Value);

            var result = new List<IAttrProperty>();
            foreach (var propertyID in propertyIDs) {
                var propertyDef = await GetEvent(propertyID);
                var property = new AttrProperty(context, propertyDef);
                result.Add(property);
            }

            return result;
        }

        public async Task<IAttrPropertyMember> GetAttrPropertyMember(int attrID, int propertyID) {
            var attrProto = await GetAttribute(attrID);

            if (null == attrProto) {
                return null;
            }

            var property = (
                from prop in events
                join attr in events on prop.Value.BaseEventID equals attr.Key
                where prop.Value.ValueID == propertyID && attr.Key == attrID
                select prop.Value
            ).SingleOrDefault();

            if (null == property) {
                return null;
            }

            return new AttrPropertyValue(context, property);
        }

        public async Task<IEnumerable<IAttrPropertyMember>> GetAttrPropertyMembers(int attrID) {
            var attrProto = await GetAttribute(attrID);

            if (null == attrProto) {
                return Array.Empty<IAttrPropertyMember>();
            }

            var properties =
                from prop in events
                join type in events on prop.Value.ValueID equals type.Key
                where prop.Value.BaseEventID == attrID && type.Value.BaseEventID == StaticEvent.AttributeProperty
                select prop.Value;

            return properties.Select(p => new AttrPropertyValue(context, p));
        }

        public async Task<IEnumerable<IIndividual>> GetAttrPropertyValues(int propertyID) {
            var propProto = await GetAttrProperty(propertyID);

            if (null == propProto) {
                return Array.Empty<IIndividual>();
            }

            var individualIDs =
                from e in events
                where e.Value.BaseEventID == propertyID && e.Value.ValueID == StaticEvent.Individual
                select e.Key;

            return await Task.WhenAll(individualIDs.Select(id => GetIndividual(id)));
        }

        public async Task<IEvent> GetAttrValue(int attrID, int valueID) {
            await Task.Yield();

            var attrValue = (
                from e in events
                where e.Value.BaseEventID == attrID &&
                    e.Value.ValueID == StaticEvent.AttributeValue &&
                    e.Key == valueID
                select e
            ).SingleOrDefault();

            return attrValue.Value;
        }

        public async Task<IEnumerable<IEvent>> GetAttrValues(int attrID) {
            await Task.Yield();

            var attrValues = (
                from e in events
                where e.Value.BaseEventID == attrID &&
                    e.Value.ValueID == StaticEvent.AttributeValue
                select e.Value
            );

            return attrValues;
        }

        public async Task<IModel> GetModel(int id) {
            var modelDef = await GetEvent(id);

            if (modelDef.ValueID != StaticEvent.Model) {
                return null;
            }

            return new Model(context, modelDef);
        }

        public async Task<IEnumerable<IModel>> GetModels() {
            var modelIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.Model
                select e.Key;

            return await Task.WhenAll(modelIDs.Select(id => GetModel(id)));
        }


        public async Task<IEvent> GetModelAttributeValueProperty(int modelID, int attributeID, int valuePropertyID) {
            var modelAttr = await GetModelAttribute(modelID, attributeID);

            if (null == modelAttr) {
                return null;
            }

            var property = (
                from a in events
                join parent in events on a.Value.ValueID equals parent.Value.ID
                where a.Value.BaseEventID == modelAttr.ID && parent.Value.BaseEventID == StaticEvent.ValueProperty && a.Value.ValueID == valuePropertyID
                select a.Value
            ).SingleOrDefault();

            return property;
        }

        public async Task<IEnumerable<IEvent>> GetModelAttributeValueProperties(int modelID, int attributeID) {
            var modelAttr = await GetModelAttribute(modelID, attributeID);

            if (null == modelAttr) {
                return Array.Empty<IEvent>();
            }

            var properties =
                from a in events
                join parent in events on a.Value.ValueID equals parent.Value.ID
                where a.Value.BaseEventID == modelAttr.ID && parent.Value.BaseEventID == StaticEvent.ValueProperty
                select a.Value;

            return properties;
        }

        public async Task<IModelAttr> GetModelAttribute(int modelID, int attrID) {
            await Task.Yield();

            var attr = (
                from e in events
                where e.Value.BaseEventID == modelID && e.Value.ValueID == StaticEvent.Attribute && e.Value.Value == attrID.ToString()
                select e.Value
            ).SingleOrDefault();

            if (null == attr) {
                return null;
            }

            return new ModelAttr(context, attr);
        }

        public async Task<IEnumerable<IModelAttr>> GetModelAttributes(int modelID) {
            var attrIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.Attribute && e.Value.BaseEventID == modelID
                select Int32.Parse(e.Value.Value);

            var attributes = await Task.WhenAll(
                attrIDs.Select(id => GetModelAttribute(modelID, id))
            );

            return attributes;
        }

        public async Task<IIndividual> GetIndividual(int id) {
            var individualDef = await GetEvent(id);

            if (individualDef.ValueID != StaticEvent.Individual) {
                return null;
            }

            return new Individual(context, individualDef);
        }

        public async Task<IEnumerable<IIndividual>> GetIndividuals() {
            var individualIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.Individual
                select e.Key;

            return await Task.WhenAll(individualIDs.Select(id => GetIndividual(id)));
        }

        public async Task<IEnumerable<string>> GetIndividualAttribute(int individualID, int attributeID) {
            await Task.Yield();

            var values =
                from e in events
                join subEvent in events
                on e.Value.ValueID
                equals subEvent.Key
                where e.Value.BaseEventID == individualID &&
                    subEvent.Value.BaseEventID == StaticEvent.Attribute &&
                    subEvent.Value.ValueID == StaticEvent.Individual &&
                    subEvent.Value.ID == attributeID
                select e.Value.Value;

            return values;
        }

        public async Task<IReadOnlyDictionary<int, IEnumerable<string>>> GetIndividualAttributes(int id) {
            await Task.Yield();

            var attributes =
                from e in events
                join subEvent in events
                on e.Value.ValueID
                equals subEvent.Key
                where e.Value.BaseEventID == id &&
                    subEvent.Value.BaseEventID == StaticEvent.Attribute &&
                    subEvent.Value.ValueID == StaticEvent.Individual
                select new {
                    ID = subEvent.Value.ID,
                    Value = e.Value.Value
                };

            var result = new Dictionary<int, IEnumerable<string>>();
            foreach (var attribute in attributes) {
                if (!result.ContainsKey(attribute.ID)) {
                    result.Add(attribute.ID, new List<string>());
                }

                var list = (List<string>)result[attribute.ID];
                list.Add(attribute.Value);
            }

            return result;
        }

        public async Task<IEnumerable<IIndividual>> GetActors() {
            var individualIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.Individual &&
                e.Value.BaseEventID == StaticEvent.Actor
                select e.Key;

            return await Task.WhenAll(individualIDs.Select(id => GetIndividual(id)));
        }

        public async Task<IEnumerable<IIndividual>> GetRoles() {
            var individualIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.Individual &&
                e.Value.BaseEventID == StaticEvent.Role
                select e.Key;

            return await Task.WhenAll(individualIDs.Select(id => GetIndividual(id)));
        }

        public async Task<IEntity> GetEntity(int id) {
            var entityDef = await GetEvent(id);

            if (entityDef.ValueID != StaticEvent.SubEvent || entityDef.BaseEventID != StaticEvent.Entity) {
                return null;
            }

            return new Entity(context, entityDef);
        }

        public async Task<IEnumerable<IEntity>> GetEntities() {
            var entityIDs =
                from e in events
                where e.Value.ValueID == StaticEvent.SubEvent &&
                e.Value.BaseEventID == StaticEvent.Entity
                select e.Key;

            return await Task.WhenAll(entityIDs.Select(id => GetEntity(id)));
        }


        public async Task<IEnumerable<IModel>> GetEntityModels(int id) {
            var modelIDs =
                from e in events
                where e.Value.BaseEventID == id && e.Value.ValueID == StaticEvent.Model
                select e.Key;

            return await Task.WhenAll(modelIDs.Select(model => GetModel(model)));
        }

        public async Task<IEnumerable<IIndividual>> GetEntityIndividuals(int id) {
            var individualIDs =
                from e in events
                where e.Value.BaseEventID == id && e.Value.ValueID == StaticEvent.Individual
                select e.Key;

            return await Task.WhenAll(individualIDs.Select(individual => GetIndividual(individual)));
        }

        public async Task<bool> IsEventAncestor(int ancestor, int checkValue) {
            var queue = new Queue<int>(new[] { checkValue });
            while (queue.Count > 0) {
                int eventID = queue.Dequeue();

                if (eventID == ancestor) {
                    return true;
                }

                var checkAncestor = await GetEvent(eventID);
                if (checkAncestor != null && eventID != checkAncestor.BaseEventID) {
                    queue.Enqueue(checkAncestor.BaseEventID);
                }
            }

            return false;
        }

        public async Task<IIndividual> GetDataTypeIndividual(string name) {
            var ev = (
                from e in events
                where e.Value.Value == name &&
                e.Value.BaseEventID == StaticEvent.DataType &&
                e.Value.ValueID == StaticEvent.Individual
                select e
            ).SingleOrDefault();

            if (null == ev.Value) {
                return null;
            }
            else {
                return await GetIndividual(ev.Key);
            }
        }

        public DataType GetDataType(string name) {
            return context.Types.Get(name);
        }
    }
}