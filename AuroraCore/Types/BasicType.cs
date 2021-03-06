// Aurora 
// Copyright (C) 2020  Frank Horrigan <https://github.com/saurer>

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Text.RegularExpressions;

namespace AuroraCore.Types {
    internal class BasicType : DataType {
        private Regex validationPattern = new Regex("^[a-zA-Z_0-9 ]+$");

        public override string Name => "basic_type";

        public override bool Validate(string data) {
            return validationPattern.IsMatch(data);
        }
    }
}