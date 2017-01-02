﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnSpy.Contracts.Hex;
using dnSpy.Contracts.Hex.Editor;
using dnSpy.Contracts.Hex.Files;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Hex.Files {
	sealed class HexFileStructureInfoServiceImpl : HexFileStructureInfoService {
		readonly HexView hexView;
		readonly HexBufferFileService hexBufferFileService;
		readonly Lazy<HexFileStructureInfoProviderFactory, IOrderable>[] hexFileStructureInfoProviderFactories;

		HexFileStructureInfoProvider[] HexFileStructureInfoProviders {
			get {
				if (hexFileStructureInfoProviders == null)
					hexFileStructureInfoProviders = CreateProviders();
				return hexFileStructureInfoProviders;
			}
		}
		HexFileStructureInfoProvider[] hexFileStructureInfoProviders;

		public HexFileStructureInfoServiceImpl(HexView hexView, HexBufferFileServiceFactory hexBufferFileServiceFactory, Lazy<HexFileStructureInfoProviderFactory, IOrderable>[] hexFileStructureInfoProviderFactories) {
			if (hexView == null)
				throw new ArgumentNullException(nameof(hexView));
			if (hexBufferFileServiceFactory == null)
				throw new ArgumentNullException(nameof(hexBufferFileServiceFactory));
			if (hexFileStructureInfoProviderFactories == null)
				throw new ArgumentNullException(nameof(hexFileStructureInfoProviderFactories));
			this.hexView = hexView;
			hexBufferFileService = hexBufferFileServiceFactory.Create(hexView.Buffer);
			this.hexFileStructureInfoProviderFactories = hexFileStructureInfoProviderFactories;
		}

		HexFileStructureInfoProvider[] CreateProviders() {
			var providers = new List<HexFileStructureInfoProvider>(hexFileStructureInfoProviderFactories.Length);
			foreach (var lz in hexFileStructureInfoProviderFactories) {
				var provider = lz.Value.Create(hexView);
				if (provider != null)
					providers.Add(provider);
			}
			return providers.ToArray();
		}

		public override HexIndexes[] GetSubStructureIndexes(HexPosition position) {
			var info = HexBufferFileUtils.GetStructure(hexBufferFileService, position);
			if (info == null)
				return null;

			var file = info.Value.Key;
			var structure = info.Value.Value;
			foreach (var provider in HexFileStructureInfoProviders) {
				var indexes = provider.GetSubStructureIndexes(file, structure, position);
				if (indexes == null)
					continue;
				bool b = IsValidIndexes(indexes, structure);
				Debug.Assert(b);
				if (b)
					return indexes;
			}
			return null;
		}

		static bool IsValidIndexes(HexIndexes[] indexes, ComplexData structure) {
			if (indexes == null)
				return false;
			if (indexes.Length == 0)
				return true;
			if ((uint)indexes[0].End > (uint)structure.FieldCount)
				return false;
			if (indexes[0].IsEmpty)
				return false;
			for (int i = 1; i < indexes.Length; i++) {
				if (indexes[i - 1].End > indexes[i].Start)
					return false;
				if ((uint)indexes[i].End > (uint)structure.FieldCount)
					return false;
				if (indexes[i].IsEmpty)
					return false;
			}
			return true;
		}

		public override object GetToolTip(HexPosition position) {
			var info = HexBufferFileUtils.GetStructure(hexBufferFileService, position);
			if (info == null)
				return null;

			var file = info.Value.Key;
			var structure = info.Value.Value;
			foreach (var provider in HexFileStructureInfoProviders) {
				var toolTip = provider.GetToolTip(info.Value.Key, info.Value.Value, position);
				if (toolTip != null)
					return toolTip;
			}

			return null;
		}

		public override object GetReference(HexPosition position) {
			var info = HexBufferFileUtils.GetStructure(hexBufferFileService, position);
			if (info == null)
				return null;

			var file = info.Value.Key;
			var structure = info.Value.Value;
			foreach (var provider in HexFileStructureInfoProviders) {
				var toolTip = provider.GetReference(info.Value.Key, info.Value.Value, position);
				if (toolTip != null)
					return toolTip;
			}

			return null;
		}

		static BufferField GetField(ComplexData structure, HexPosition position) {
			for (;;) {
				var field = structure.GetFieldByPosition(position);
				if (field == null)
					return null;
				structure = field.Data as ComplexData;
				if (structure == null)
					return field;
			}
		}

		public override HexSpan? GetFieldReferenceSpan(HexPosition position) {
			var info = HexBufferFileUtils.GetStructure(hexBufferFileService, position);
			if (info == null)
				return null;

			var file = info.Value.Key;
			var structure = info.Value.Value;

			HexSpan? span;
			foreach (var provider in HexFileStructureInfoProviders) {
				span = provider.GetFieldReferenceSpan(info.Value.Key, info.Value.Value, position);
				if (span != null)
					return span;
			}

			var field = GetField(structure, position);
			span = (field?.Data as SimpleData)?.GetFieldReferenceSpan(file);
			if (span != null)
				return span;

			return null;
		}
	}
}
