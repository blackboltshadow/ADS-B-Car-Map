using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the filter tree edited in the filter editor, of the sentence that describes a filter and of the order of evaluation.
    /// </summary>
    public class FilterDraftTests
    {
        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Creates a condition node.
        /// </summary>
        /// <param name="field">Tested property.</param>
        /// <param name="text">Text operand.</param>
        /// <param name="negated">Whether the condition is negated.</param>
        /// <returns>The condition.</returns>
        private static FilterDraftCondition Condition(FilterField field, string text = "", bool negated = false)
        {
            return new FilterDraftCondition { Field = field, Text = text, Negated = negated };
        }

        /// <summary>
        /// Creates a group node with children.
        /// </summary>
        /// <param name="mode">Way the children are combined.</param>
        /// <param name="negated">Whether the group is negated.</param>
        /// <param name="children">Children.</param>
        /// <returns>The group.</returns>
        private static FilterDraftGroup Group(FilterGroupMode mode, bool negated, params FilterDraftNode[] children)
        {
            FilterDraftGroup group = new FilterDraftGroup { Mode = mode, Negated = negated };

            foreach (FilterDraftNode child in children)
            {
                group.Add(child);
            }

            return group;
        }

        /// <summary>
        /// Creates the draft of "(B744 and LOT and not on the ground) or (B744 and CHG)".
        /// </summary>
        /// <returns>The draft.</returns>
        private static FilterDraft CreateNestedDraft()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Dwa warianty";
            draft.Root.Mode = FilterGroupMode.Any;
            draft.Root.Add(Group(FilterGroupMode.All, false, Condition(FilterField.TypeCode, "B744"), Condition(FilterField.CallsignPrefix, "LOT"), Condition(FilterField.OnGround, string.Empty, true)));
            draft.Root.Add(Group(FilterGroupMode.All, false, Condition(FilterField.TypeCode, "B744"), Condition(FilterField.CallsignPrefix, "CHG")));

            return draft;
        }

        /// <summary>
        /// Creates an aircraft with the given properties.
        /// </summary>
        /// <param name="callsign">Callsign.</param>
        /// <param name="typeCode">ICAO type code.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string callsign, string typeCode, bool onGround = false)
        {
            return new Aircraft("1", callsign, 52.0, 21.0, onGround ? null : 35000, onGround, 450.0, 90.0, 0.0, typeCode, null);
        }

        /// <summary>
        /// A correct draft has no problem.
        /// </summary>
        [Fact]
        public void Validate_AcceptsCorrectTree()
        {
            Assert.Null(CreateNestedDraft().Validate());
        }

        /// <summary>
        /// The filter built from a nested draft evaluates like the hand-made expression.
        /// </summary>
        [Fact]
        public void ToFilter_NestedTreeEvaluates()
        {
            AircraftFilter filter = CreateNestedDraft().ToFilter("x");

            Assert.Equal(FilterResult.True, filter.Expression.Evaluate(CreateAircraft("LOT1", "B744"), NoRoutes));
            Assert.Equal(FilterResult.True, filter.Expression.Evaluate(CreateAircraft("CHG7", "B744"), NoRoutes));
            Assert.Equal(FilterResult.False, filter.Expression.Evaluate(CreateAircraft("LOT1", "B744", true), NoRoutes));
            Assert.Equal(FilterResult.False, filter.Expression.Evaluate(CreateAircraft("CHG7", "A320"), NoRoutes));
        }

        /// <summary>
        /// A negated group negates the whole group.
        /// </summary>
        [Fact]
        public void ToFilter_NegatedGroup()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Poza WAW i KRK";
            draft.Root.Add(Condition(FilterField.Manufacturer, "Boeing"));
            draft.Root.Add(Group(FilterGroupMode.Any, true, Condition(FilterField.Destination, "WAW"), Condition(FilterField.Destination, "KRK")));

            AircraftFilter filter = draft.ToFilter("x");
            Aircraft boeing = CreateAircraft("LOT1", "B738");
            FlightRoute toWarsaw = new FlightRoute("RZE", "Rzeszów", "WAW", "Warszawa");
            FlightRoute toGdansk = new FlightRoute("RZE", "Rzeszów", "GDN", "Gdańsk");

            Assert.Equal(FilterResult.False, filter.Expression.Evaluate(boeing, (string? callsign) => toWarsaw));
            Assert.Equal(FilterResult.True, filter.Expression.Evaluate(boeing, (string? callsign) => toGdansk));
            Assert.Equal(FilterResult.Unknown, filter.Expression.Evaluate(boeing, NoRoutes));
        }

        /// <summary>
        /// A negated root group is built as a negation of the whole tree.
        /// </summary>
        [Fact]
        public void ToFilter_NegatedRoot()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Nie wojskowe";
            draft.Root.Negated = true;
            draft.Root.Add(Condition(FilterField.Military));

            Assert.IsType<FilterNot>(draft.ToFilter("x").Expression);
        }

        /// <summary>
        /// Every kind of problem is reported in Polish together with the node it refers to.
        /// </summary>
        [Fact]
        public void Validate_ReportsProblemsWithNode()
        {
            FilterDraft draft = new FilterDraft();

            Assert.Equal("Podaj nazwę filtru.", draft.Validate(out FilterDraftNode? node));
            Assert.Null(node);

            draft.Name = new string('a', FilterDraft.MaxNameLength + 1);
            Assert.NotNull(draft.Validate());

            draft.Name = "Test";
            Assert.Equal("Dodaj co najmniej jeden warunek.", draft.Validate(out node));
            Assert.Same(draft.Root, node);

            FilterDraftCondition destination = Condition(FilterField.Destination, "  ");
            draft.Root.Add(destination);
            Assert.Equal("Uzupełnij wartość: Lotnisko docelowe.", draft.Validate(out node));
            Assert.Same(destination, node);

            destination.Text = "RZE";
            FilterDraftGroup empty = Group(FilterGroupMode.All, false);
            draft.Root.Add(empty);
            Assert.StartsWith("Grupa jest pusta", draft.Validate(out node));
            Assert.Same(empty, node);

            FilterDraftCondition altitude = new FilterDraftCondition { Field = FilterField.Altitude };
            empty.Add(altitude);
            Assert.StartsWith("Podaj wysokość", draft.Validate(out node));
            Assert.Same(altitude, node);

            altitude.Minimum = 5000.0;
            altitude.Maximum = 1000.0;
            Assert.StartsWith("Wysokość „od” jest większa", draft.Validate());

            altitude.Maximum = 9000.0;
            Assert.Null(draft.Validate());
        }

        /// <summary>
        /// Conditions without an operand need no value.
        /// </summary>
        [Fact]
        public void Validate_FlagConditionsNeedNoValue()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Ziemia";
            draft.Mode = FilterMode.Hide;
            draft.Root.Add(Condition(FilterField.OnGround));

            Assert.Null(draft.Validate());
        }

        /// <summary>
        /// A tree deeper than the limit is rejected, and one at the limit is accepted.
        /// </summary>
        [Fact]
        public void Validate_LimitsDepth()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Głęboki";

            FilterDraftGroup current = draft.Root;

            for (int level = 0; level < FilterDraft.MaxDepth - 1; level++)
            {
                FilterDraftGroup inner = Group(FilterGroupMode.All, false);

                current.Add(inner);
                current = inner;
            }

            current.Add(Condition(FilterField.Military));
            Assert.Null(draft.Validate());

            FilterDraftGroup tooDeep = Group(FilterGroupMode.All, false, Condition(FilterField.Military));

            current.Add(tooDeep);
            Assert.Contains("zbyt głęboko", draft.Validate());
        }

        /// <summary>
        /// A tree at the depth limit survives a trip through JSON, so a saved filter can always be read back.
        /// </summary>
        [Fact]
        public void Json_DeepestAllowedTreeRoundTrips()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Głęboki";
            draft.Root.Negated = true;

            FilterDraftGroup current = draft.Root;

            for (int level = 0; level < FilterDraft.MaxDepth - 1; level++)
            {
                FilterDraftGroup inner = Group(FilterGroupMode.Any, true);

                current.Add(inner);
                current = inner;
            }

            current.Add(Condition(FilterField.Military, string.Empty, true));
            Assert.Null(draft.Validate());

            AircraftFilter filter = draft.ToFilter("deep");
            List<AircraftFilter> parsed = FilterJson.Parse(FilterJson.Serialize(new List<AircraftFilter> { filter }));

            Assert.Single(parsed);
            Assert.Equal(FilterDescriber.Describe(filter), FilterDescriber.Describe(parsed[0]));
        }

        /// <summary>
        /// Every predefined filter can be opened in the editor and rebuilt without changing the description.
        /// </summary>
        [Fact]
        public void FromFilter_RoundTripsPredefinedFilters()
        {
            foreach (AircraftFilter filter in BuiltInFilters.All)
            {
                FilterDraft? draft = FilterDraft.FromFilter(filter);

                Assert.NotNull(draft);
                Assert.Equal(FilterDescriber.Describe(filter), FilterDescriber.Describe(draft.ToFilter(filter.Id)));
            }
        }

        /// <summary>
        /// An expression with nested groups and negations can be opened and rebuilt.
        /// </summary>
        [Fact]
        public void FromFilter_ReadsNestedExpression()
        {
            AircraftFilter nested = new AircraftFilter(
                "n",
                "Nested",
                FilterMode.Show,
                FilterGroup.All(FilterCondition.Military(), new FilterNot(FilterGroup.Any(FilterCondition.OnGround(), FilterCondition.OfType("B738")))));

            FilterDraft? draft = FilterDraft.FromFilter(nested);

            Assert.NotNull(draft);
            Assert.Equal(2, draft.Root.Children.Count);

            FilterDraftGroup inner = Assert.IsType<FilterDraftGroup>(draft.Root.Children[1]);

            Assert.True(inner.Negated);
            Assert.Equal(FilterGroupMode.Any, inner.Mode);
            Assert.Equal(FilterDescriber.Describe(nested), FilterDescriber.Describe(draft.ToFilter("n")));
        }

        /// <summary>
        /// A bare negated condition is opened as one negated node inside the root group, and two negations cancel out.
        /// </summary>
        [Fact]
        public void FromFilter_FoldsNegations()
        {
            FilterDraft? negated = FilterDraft.FromFilter(new AircraftFilter("n", "Nie wojskowe", FilterMode.Show, new FilterNot(FilterCondition.Military())));
            FilterDraft? doubled = FilterDraft.FromFilter(new AircraftFilter("d", "Dwa razy", FilterMode.Show, new FilterNot(new FilterNot(FilterCondition.Military()))));

            Assert.NotNull(negated);
            Assert.Single(negated.Root.Children);
            Assert.True(negated.Root.Children[0].Negated);
            Assert.NotNull(doubled);
            Assert.False(doubled.Root.Children[0].Negated);
        }

        /// <summary>
        /// A negated group at the top of an expression becomes the negated root.
        /// </summary>
        [Fact]
        public void FromFilter_NegatedTopGroupBecomesRoot()
        {
            FilterDraft? draft = FilterDraft.FromFilter(new AircraftFilter("n", "N", FilterMode.Hide, new FilterNot(FilterGroup.Any(FilterCondition.Military(), FilterCondition.OnGround()))));

            Assert.NotNull(draft);
            Assert.True(draft.Root.Negated);
            Assert.Equal(FilterGroupMode.Any, draft.Root.Mode);
            Assert.Equal(2, draft.Root.Children.Count);
        }

        /// <summary>
        /// A new draft has one empty condition, which is not valid until it is filled in.
        /// </summary>
        [Fact]
        public void CreateNew_HasOneEmptyCondition()
        {
            FilterDraft draft = FilterDraft.CreateNew();

            draft.Name = "Nowy";

            Assert.Single(draft.Root.Children);
            Assert.NotNull(draft.Validate());
        }

        /// <summary>
        /// Moving a node up and down changes its position inside the group and stops at the ends.
        /// </summary>
        [Fact]
        public void Move_UpAndDownInsideGroup()
        {
            FilterDraft draft = new FilterDraft();
            FilterDraftCondition first = Condition(FilterField.Military);
            FilterDraftCondition second = Condition(FilterField.OnGround);
            FilterDraftCondition third = Condition(FilterField.TypeCode, "B738");

            draft.Root.Add(first);
            draft.Root.Add(second);
            draft.Root.Add(third);

            Assert.False(draft.CanMoveUp(first));
            Assert.True(draft.CanMoveDown(first));
            Assert.True(draft.MoveDown(first));
            Assert.Equal(new FilterDraftNode[] { second, first, third }, draft.Root.Children);
            Assert.True(draft.MoveUp(third));
            Assert.Equal(new FilterDraftNode[] { second, third, first }, draft.Root.Children);
            Assert.False(draft.MoveDown(first));
            Assert.False(draft.MoveUp(second));
            Assert.False(draft.CanMoveDown(first));
        }

        /// <summary>
        /// The root cannot be moved, removed or copied.
        /// </summary>
        [Fact]
        public void Root_CannotBeMovedRemovedOrCopied()
        {
            FilterDraft draft = new FilterDraft();

            Assert.False(draft.MoveUp(draft.Root));
            Assert.False(draft.MoveDown(draft.Root));
            Assert.False(draft.CanMoveUp(draft.Root));
            Assert.False(draft.CanMoveDown(draft.Root));
            Assert.False(draft.Remove(draft.Root));
            Assert.Null(draft.Duplicate(draft.Root));
            Assert.False(draft.CanMoveTo(draft.Root, draft.Root));
        }

        /// <summary>
        /// A node can be moved to another group, but not into itself or into a group inside it.
        /// </summary>
        [Fact]
        public void MoveTo_AnotherGroupButNotIntoItself()
        {
            FilterDraft draft = CreateNestedDraft();
            FilterDraftGroup firstGroup = (FilterDraftGroup)draft.Root.Children[0];
            FilterDraftGroup secondGroup = (FilterDraftGroup)draft.Root.Children[1];
            FilterDraftNode moved = firstGroup.Children[1];
            FilterDraftGroup inner = Group(FilterGroupMode.Any, false, Condition(FilterField.Military));

            secondGroup.Add(inner);

            Assert.True(draft.CanMoveTo(moved, secondGroup));
            Assert.True(draft.MoveTo(moved, secondGroup));
            Assert.Same(secondGroup, moved.Parent);
            Assert.Same(moved, secondGroup.Children[secondGroup.Children.Count - 1]);
            Assert.Equal(2, firstGroup.Children.Count);

            Assert.False(draft.CanMoveTo(secondGroup, secondGroup));
            Assert.False(draft.CanMoveTo(secondGroup, inner));
            Assert.False(draft.MoveTo(secondGroup, inner));
            Assert.True(draft.CanMoveTo(inner, draft.Root));
        }

        /// <summary>
        /// A copy of a group is inserted right after it and does not share children with the original.
        /// </summary>
        [Fact]
        public void Duplicate_InsertsIndependentCopy()
        {
            FilterDraft draft = CreateNestedDraft();
            FilterDraftGroup original = (FilterDraftGroup)draft.Root.Children[0];

            FilterDraftNode? copy = draft.Duplicate(original);

            Assert.NotNull(copy);
            Assert.Equal(3, draft.Root.Children.Count);
            Assert.Same(copy, draft.Root.Children[1]);
            Assert.NotSame(original, copy);
            Assert.Same(draft.Root, copy.Parent);

            FilterDraftGroup copiedGroup = Assert.IsType<FilterDraftGroup>(copy);

            Assert.Equal(original.Children.Count, copiedGroup.Children.Count);
            Assert.NotSame(original.Children[0], copiedGroup.Children[0]);

            ((FilterDraftCondition)copiedGroup.Children[0]).Text = "A320";

            Assert.Equal("B744", ((FilterDraftCondition)original.Children[0]).Text);
        }

        /// <summary>
        /// Removing a node removes it from its group.
        /// </summary>
        [Fact]
        public void Remove_TakesNodeOutOfItsGroup()
        {
            FilterDraft draft = CreateNestedDraft();
            FilterDraftNode first = draft.Root.Children[0];

            Assert.True(draft.Remove(first));
            Assert.Single(draft.Root.Children);
            Assert.Null(first.Parent);
            Assert.False(draft.Remove(first));
        }

        /// <summary>
        /// The depth and the list of groups follow the tree, root first.
        /// </summary>
        [Fact]
        public void Tree_DepthAndGroups()
        {
            FilterDraft draft = CreateNestedDraft();
            FilterDraftGroup firstGroup = (FilterDraftGroup)draft.Root.Children[0];

            Assert.Equal(0, FilterDraft.GetDepth(draft.Root));
            Assert.Equal(1, FilterDraft.GetDepth(firstGroup));
            Assert.Equal(2, FilterDraft.GetDepth(firstGroup.Children[0]));

            IReadOnlyList<FilterDraftGroup> groups = draft.GetGroups();

            Assert.Equal(3, groups.Count);
            Assert.Same(draft.Root, groups[0]);
            Assert.Same(firstGroup, groups[1]);
        }

        /// <summary>
        /// Cloning a condition keeps all its operands.
        /// </summary>
        [Fact]
        public void Clone_CopiesConditionOperands()
        {
            FilterDraftCondition condition = new FilterDraftCondition { Field = FilterField.Altitude, Negated = true, Minimum = 10.0, Maximum = 20.0, Text = "t", Kind = AircraftKind.Fighter };
            FilterDraftCondition copy = (FilterDraftCondition)condition.Clone();

            Assert.Equal(FilterField.Altitude, copy.Field);
            Assert.True(copy.Negated);
            Assert.Equal(10.0, copy.Minimum);
            Assert.Equal(20.0, copy.Maximum);
            Assert.Equal("t", copy.Text);
            Assert.Equal(AircraftKind.Fighter, copy.Kind);
            Assert.Null(copy.Parent);
        }

        /// <summary>
        /// The sentence of the example filter reads naturally.
        /// </summary>
        [Fact]
        public void Sentence_DescribesExample()
        {
            FilterDraft draft = new FilterDraft();

            draft.Name = "Boeing poza WAW i KRK";
            draft.Root.Add(Condition(FilterField.Manufacturer, "Boeing"));
            draft.Root.Add(Group(FilterGroupMode.Any, true, Condition(FilterField.Destination, "WAW"), Condition(FilterField.Destination, "KRK")));
            draft.Root.Add(Condition(FilterField.OnGround, string.Empty, true));

            Assert.Equal(
                "Pokazuje każdy samolot, który pochodzi od producenta Boeing oraz nie leci do lotniska WAW oraz nie leci do lotniska KRK oraz nie jest na ziemi.",
                FilterDescriber.DescribeSentence(draft.ToFilter("x")));
        }

        /// <summary>
        /// A negated OR group becomes an AND of negations, and a negated AND group becomes an OR of negations.
        /// </summary>
        [Fact]
        public void Sentence_AppliesDeMorgan()
        {
            AircraftFilter negatedAnd = new AircraftFilter("a", "A", FilterMode.Hide, new FilterNot(FilterGroup.All(FilterCondition.Military(), FilterCondition.OnGround())));

            Assert.Equal("Ukrywa każdy samolot, który nie jest wojskowy lub nie jest na ziemi.", FilterDescriber.DescribeSentence(negatedAnd));
        }

        /// <summary>
        /// Nested groups of different kinds are put in parentheses, and same-kind groups are merged.
        /// </summary>
        [Fact]
        public void Sentence_ParenthesesAndMerging()
        {
            AircraftFilter mixed = new AircraftFilter(
                "m",
                "M",
                FilterMode.Show,
                FilterGroup.Any(FilterGroup.All(FilterCondition.OfType("B744"), FilterCondition.CallsignStartsWith("LOT")), FilterGroup.Any(FilterCondition.Military(), FilterCondition.OnGround())));

            Assert.Equal(
                "Pokazuje każdy samolot, który (ma typ B744 oraz ma znak zaczynający się od LOT) lub jest wojskowy lub jest na ziemi.",
                FilterDescriber.DescribeSentence(mixed));
        }

        /// <summary>
        /// Every field has a phrase, in the affirmative and in the negative.
        /// </summary>
        [Fact]
        public void Sentence_CoversAllFields()
        {
            Assert.Equal("Pokazuje każdy samolot, który ma rodzaj: śmigłowiec.", FilterDescriber.DescribeSentence(new AircraftFilter("1", "1", FilterMode.Show, FilterCondition.OfKind(AircraftKind.Helicopter))));
            Assert.Equal("Pokazuje każdy samolot, który nie ma rodzaju: śmigłowiec.", FilterDescriber.DescribeSentence(new AircraftFilter("1", "1", FilterMode.Show, new FilterNot(FilterCondition.OfKind(AircraftKind.Helicopter)))));
            Assert.Equal("Pokazuje każdy samolot, który nie ma typu B738.", FilterDescriber.DescribeSentence(new AircraftFilter("2", "2", FilterMode.Show, new FilterNot(FilterCondition.OfType("B738")))));
            Assert.Equal("Pokazuje każdy samolot, który nie pochodzi od producenta Airbus.", FilterDescriber.DescribeSentence(new AircraftFilter("3", "3", FilterMode.Show, new FilterNot(FilterCondition.MadeBy("Airbus")))));
            Assert.Equal("Pokazuje każdy samolot, który leci z lotniska WAW.", FilterDescriber.DescribeSentence(new AircraftFilter("4", "4", FilterMode.Show, FilterCondition.FromAirport("WAW"))));
            Assert.Equal("Pokazuje każdy samolot, który nie leci z lotniska WAW.", FilterDescriber.DescribeSentence(new AircraftFilter("4", "4", FilterMode.Show, new FilterNot(FilterCondition.FromAirport("WAW")))));
            Assert.Equal("Pokazuje każdy samolot, który leci na wysokości od 300 m.", FilterDescriber.DescribeSentence(new AircraftFilter("5", "5", FilterMode.Show, FilterCondition.AltitudeBetween(300.0, null))));
            Assert.Equal("Pokazuje każdy samolot, który nie leci na wysokości 100–200 m.", FilterDescriber.DescribeSentence(new AircraftFilter("5", "5", FilterMode.Show, new FilterNot(FilterCondition.AltitudeBetween(100.0, 200.0)))));
            Assert.Equal("Pokazuje każdy samolot, który leci na dowolnej wysokości.", FilterDescriber.DescribeSentence(new AircraftFilter("5", "5", FilterMode.Show, FilterCondition.AltitudeBetween(null, null))));
            Assert.Equal("Pokazuje każdy samolot, który jest wojskowy.", FilterDescriber.DescribeSentence(new AircraftFilter("6", "6", FilterMode.Show, FilterCondition.Military())));
            Assert.Equal("Pokazuje każdy samolot, który nie ma znaku zaczynającego się od LOT.", FilterDescriber.DescribeSentence(new AircraftFilter("7", "7", FilterMode.Show, new FilterNot(FilterCondition.CallsignStartsWith("LOT")))));
            Assert.Equal("Ukrywa każdy samolot, który jest na ziemi.", FilterDescriber.DescribeSentence(BuiltInFilters.HideOnGround));
        }

        /// <summary>
        /// A filter nested deeper than the limit has no sentence, and a group with one child needs no parentheses.
        /// </summary>
        [Fact]
        public void Sentence_NullWhenTooDeep()
        {
            FilterNode node = FilterCondition.Military();

            for (int level = 0; level < FilterDescriber.MaxSentenceDepth; level++)
            {
                node = FilterGroup.All(node, FilterCondition.OnGround());
                node = FilterGroup.Any(node, FilterCondition.OfType("B738"));
            }

            Assert.Null(FilterDescriber.DescribeSentence(new AircraftFilter("d", "D", FilterMode.Show, node)));
            Assert.NotNull(FilterDescriber.DescribeSentence(new AircraftFilter("s", "S", FilterMode.Show, FilterGroup.All(FilterGroup.Any(FilterCondition.Military())))));
        }

        /// <summary>
        /// An empty group is described instead of producing an empty sentence.
        /// </summary>
        [Fact]
        public void Sentence_EmptyGroup()
        {
            Assert.Equal("Pokazuje każdy samolot, który (pusta grupa).", FilterDescriber.DescribeSentence(new AircraftFilter("e", "E", FilterMode.Show, FilterGroup.All())));
        }

        /// <summary>
        /// Conditions on the route are evaluated after the cheap ones, whatever their order in the group.
        /// </summary>
        [Fact]
        public void Evaluation_RouteConditionsGoLast()
        {
            int lookups = 0;
            Func<string?, FlightRoute?> lookup = (string? callsign) =>
            {
                lookups++;

                return null;
            };
            Aircraft airbus = CreateAircraft("LOT1", "A320");
            Aircraft boeing = CreateAircraft("LOT1", "B738");

            FilterNode andGroup = FilterGroup.All(FilterCondition.ToAirport("RZE"), FilterCondition.OfType("B738"));
            FilterNode orGroup = FilterGroup.Any(FilterCondition.ToAirport("RZE"), FilterCondition.OfType("B738"));

            Assert.Equal(FilterResult.False, andGroup.Evaluate(airbus, lookup));
            Assert.Equal(FilterResult.True, orGroup.Evaluate(boeing, lookup));
            Assert.Equal(0, lookups);

            Assert.Equal(FilterResult.Unknown, andGroup.Evaluate(boeing, lookup));
            Assert.Equal(1, lookups);
        }

        /// <summary>
        /// The result of a group does not depend on the order of its children.
        /// </summary>
        [Fact]
        public void Evaluation_ResultIndependentOfOrder()
        {
            FlightRoute toRzeszow = new FlightRoute("WAW", "Warszawa", "RZE", "Rzeszów");
            Func<string?, FlightRoute?> lookup = (string? callsign) => toRzeszow;
            Aircraft boeing = CreateAircraft("LOT1", "B738");
            FilterNode[] pieces = new FilterNode[] { FilterCondition.ToAirport("RZE"), FilterCondition.OfType("B738"), new FilterNot(FilterCondition.OnGround()) };
            FilterNode[] reversed = new FilterNode[] { pieces[2], pieces[1], pieces[0] };

            Assert.Equal(new FilterGroup(FilterGroupMode.All, pieces).Evaluate(boeing, lookup), new FilterGroup(FilterGroupMode.All, reversed).Evaluate(boeing, lookup));
            Assert.Equal(new FilterGroup(FilterGroupMode.Any, pieces).Evaluate(boeing, NoRoutes), new FilterGroup(FilterGroupMode.Any, reversed).Evaluate(boeing, NoRoutes));
        }

        /// <summary>
        /// A node needs the route when it, or something inside it, tests the origin or the destination.
        /// </summary>
        [Fact]
        public void NeedsRoute_PropagatesThroughGroupsAndNegations()
        {
            Assert.True(FilterCondition.ToAirport("RZE").NeedsRoute);
            Assert.True(FilterCondition.FromAirport("WAW").NeedsRoute);
            Assert.False(FilterCondition.Military().NeedsRoute);
            Assert.True(new FilterNot(FilterCondition.ToAirport("RZE")).NeedsRoute);
            Assert.True(FilterGroup.All(FilterCondition.Military(), FilterGroup.Any(FilterCondition.ToAirport("RZE"))).NeedsRoute);
            Assert.False(FilterGroup.All(FilterCondition.Military(), FilterCondition.OnGround()).NeedsRoute);
            Assert.True(BuiltInFilters.ToRzeszow.Expression.NeedsRoute);
            Assert.False(BuiltInFilters.Military.Expression.NeedsRoute);
        }
    }
}
