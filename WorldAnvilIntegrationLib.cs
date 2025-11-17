/**************************************************************************************************************************
* Copyright 2025, Peter R. Nelson
*
* This file is part of the World Anvil Integration Library. The World Anvil Integration Library allows C# applications
* to make use of the World Anvil API accessed through web HTTPS endpoints. The World Anvil API documentation can be
* found at: https://www.worldanvil.com/api/external/boromir/documentation
* 
* The World Anvil Integration Library is free software: you can redistribute it and/or modify it under the terms
* of the GNU General Public License as published by the Free Software Foundation,
* either version 3 of the License, or (at your option) any later version.
*
* This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
* without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
* See the GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License along with this program.
* The text of the GNU General Public License (GPL) is found in the LICENSE file.
* If the LICENSE file is not present or the text of the GNU GPL is not present in the LICENSE file,
* see https://www.gnu.org/licenses/.
*
* For questions about the World Anvil Integration Library or about licensing, please email
* support@brookmonte.com
*
***************************************************************************************************************************/
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldAnvilIntegrationLib
{
    public class WorldAnvilApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static WorldAnvilApiResponse<T> FromSuccess(T data)
            => new() { Success = true, Data = data };

        public static WorldAnvilApiResponse<T> FromError(string message)
            => new() { Success = false, ErrorMessage = message };
    }

    public class WorldAnvilApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public WorldAnvilApiClient()
        {
            _httpClient = new HttpClient();

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        ~WorldAnvilApiClient()
        {
            _httpClient.Dispose();
        }

        public HttpClient HttpClient => _httpClient;

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class WorldAnvilApiMethods
    {
        private readonly WorldAnvilApiClient _waApiClient;

        private string _waAPIKey = string.Empty;
        private string _waUserApiToken = string.Empty;
        private const string WaApiBaseUrl = "https://www.worldanvil.com/api/external/boromir/";
        private const string CloudflairWorkerBaseUrl = "https://realm-studio-world-anvil-integration.nelson-peter-r.workers.dev/";

        public WorldAnvilApiMethods()
        {
            _waApiClient = new();
        }

        public WorldAnvilApiClient WorldAnvilApiClient => _waApiClient ?? throw new InvalidOperationException("WorldAnvilApiClient is not initialized.");

        public string WorldAnvilAPIKey
        {
            get => _waAPIKey;
            set => _waAPIKey = value ?? throw new ArgumentNullException(nameof(WorldAnvilAPIKey), "WorldAnvilAPIKey cannot be null.");
        }

        public string WorldAnvilUserApiToken
        {
            get => _waUserApiToken;
            set => _waUserApiToken = value ?? throw new ArgumentNullException(nameof(WorldAnvilUserApiToken), "WorldAnvilUserApiToken cannot be null.");
        }


        public void SetWorldAnvilCredentials(string userApiToken)
        {
            WorldAnvilUserApiToken = userApiToken;
        }

        //==============================================================================================//
        // Public Methods
        //==============================================================================================//
        // Retrieves the Realm Studio World Anvil API Key from the brookmonte.com endpoint using the provided JWT token
        public async Task<string?> GetWorldAnvilAPIKey()
        {
            // Implementation to retrieve the API key
            string? token = await GetJwtTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                string? waApiKeyResponse = await GetWAApiKey(token);

                if (!string.IsNullOrEmpty(waApiKeyResponse))
                {
                    WorldAnvilAPIKey = waApiKeyResponse;
                }
                else
                {
                    throw new Exception("Failed to retrieve World Anvil API Key from the endpoint.");
                }
            }
            else
            {
                throw new Exception("Failed to retrieve token for API integration.");
            }

            return null;
        }

        //==============================================================================================//
        // Public World Anvil API Methods
        //==============================================================================================//
        // See World Anvil API documentation at
        // https://www.worldanvil.com/api/external/boromir/swagger-documentation#/ for details.
        //==============================================================================================//
        // ARTICLE METHODS

        //   GET /api/external/boromir/article?id={articleId}&granularity={granularity}
        public JsonDocument GetArticleById(string articleId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(articleId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 1 || granularity == 2 || granularity == 3, true, nameof(granularity));

            return DoGetAsync("article", articleId, granularity).Result;
        }

        public WorldAnvilArticle? GetWorldAnvilArticleObjectById(string articleId, int granularity)
        {
            try
            {
                JsonDocument? result = GetArticleById(articleId, granularity) ?? throw new Exception("GetArticleById returned null");

                string resultString = result.RootElement.GetRawText();

                // serialize the JSON into the WorldAnvilUser object
                WorldAnvilArticle? worldAnvilArticle = JsonDocumentExtensions.ToObject<WorldAnvilArticle>(result, JsonDocumentExtensions.JsonConversionOptions);

                if (worldAnvilArticle != null)
                {
                    worldAnvilArticle.granularity = granularity;
                }

                return worldAnvilArticle;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        //  PUT /api/external/boromir/article
        public JsonDocument CreateArticle(JsonDocument articleData)
        {
            return DoPutAsync<JsonDocument>("article", articleData).Result;
        }

        //   DELETE /api/external/boromir/article?id={articleId}
        public bool DeleteArticle(string articleId)
        {
            return DoDeleteAsync<bool>("article", articleId).Result;
        }

        //   PATCH /api/external/boromir/article?id={articleId}
        public JsonDocument UpdateArticle(string articleId, JsonDocument articleData)
        {
            return DoPatchAsync<JsonDocument>("article", articleId, articleData).Result;
        }

        //   POST /api/external/boromir/world/articles?id={worldId}
        public List<JsonDocument> ListArticlesForWorld(string worldId, int limit, int offset, string? categoryId = null)
        {
            return DoPostAsync<List<JsonDocument>>("world/articles", worldId, limit, offset, categoryId).Result;
        }

        //==============================================================================================//
        // BLOCK METHODS

        //   GET /api/external/boromir/block?id={blockId}&granularity={granularity}
        public JsonDocument GetBlockById(string blockId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("block", blockId, granularity).Result;
        }

        //   PUT /api/external/boromir/block
        public JsonDocument CreateBlock(JsonDocument blockData)
        {
            return DoPutAsync<JsonDocument>("block", blockData).Result;
        }

        //   DELETE /api/external/boromir/block?id={blockId}
        public bool DeleteBlock(string blockId)
        {
            return DoDeleteAsync<bool>("block", blockId).Result;
        }

        //   PATCH /api/external/boromir/block?id={blockId}
        public JsonDocument UpdateBlock(string blockId, JsonDocument blockData)
        {
            return DoPatchAsync<JsonDocument>("block", blockId, blockData).Result;
        }

        //   POST /api/external/boromir/blockfolder/blocks?id={blockFolderId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListBlocksInBlockFolder(string blockFolderId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("blockfolder/blocks", blockFolderId, limit, offset).Result;
        }

        //==============================================================================================//
        // BLOCK FOLDER METHODS

        //   GET /api/external/boromir/blockfolder?id={blockFolderId}&granularity={granularity}
        public JsonDocument GetBlockFolderById(string blockFolderId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockFolderId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("blockfolder", blockFolderId, granularity).Result;
        }

        //   PUT /api/external/boromir/blockfolder
        public JsonDocument CreateBlockFolder(JsonDocument blockFolderData)
        {
            return DoPutAsync<JsonDocument>("blockfolder", blockFolderData).Result;
        }

        //   DELETE /api/external/boromir/blockfolder?id={blockFolderId}
        public bool DeleteBlockFolder(string blockFolderId)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockFolderId);
            return DoDeleteAsync<bool>("blockfolder", blockFolderId).Result;
        }

        //   PATCH /api/external/boromir/blockfolder?id={blockFolderId}
        public JsonDocument UpdateBlockFolder(string blockFolderId, JsonDocument blockFolderData)
        {
            return DoPatchAsync<JsonDocument>("blockfolder", blockFolderId, blockFolderData).Result;
        }

        //   POST /api/external/boromir/world/blockfolders?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListBlockFoldersForWorld(string worldId, int limit, int offset)
        {
            ArgumentException.ThrowIfNullOrEmpty(worldId);
            return DoPostAsync<List<JsonDocument>>("world/blockfolders", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // BLOCK TEMPLATE METHODS

        //   GET /api/external/boromir/blocktemplate?id={blockTemplateId}&granularity={granularity}
        public JsonDocument GetBlockTemplateById(string blockTemplateId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockTemplateId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 1 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("blocktemplate", blockTemplateId, granularity).Result;
        }

        //   PUT /api/external/boromir/blocktemplate
        public JsonDocument CreateBlockTemplate(JsonDocument blockTemplateData)
        {
            return DoPutAsync<JsonDocument>("blocktemplate", blockTemplateData).Result;
        }

        //   DELETE /api/external/boromir/blocktemplate?id={blockTemplateId}
        public bool DeleteBlockTemplate(string blockTemplateId)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockTemplateId);
            return DoDeleteAsync<bool>("blocktemplate", blockTemplateId).Result;
        }

        //   PATCH /api/external/boromir/blocktemplate?id={blockTemplateId}
        public JsonDocument UpdateBlockTemplate(string blockTemplateId, JsonDocument blockTemplateData)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockTemplateId);
            return DoPatchAsync<JsonDocument>("blocktemplate", blockTemplateId, blockTemplateData).Result;
        }

        //   POST /api/external/boromir/user/blocktemplates?id={userId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListBlockTemplatesForUser(string userId, int limit, int offset)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            return DoPostAsync<List<JsonDocument>>("user/blocktemplates", userId, limit, offset).Result;
        }

        //==============================================================================================//
        // BLOCK TEMPLATE PART METHODS

        //   GET /api/external/boromir/blocktemplatepart?id={blockTemplatePartId}&granularity={granularity}
        public JsonDocument GetBlockTemplatePartById(string blockTemplatePartId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(blockTemplatePartId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("blocktemplatepart", blockTemplatePartId, granularity).Result;
        }

        //   PUT /api/external/boromir/blocktemplatepart
        public JsonDocument CreateBlockTemplatePart(JsonDocument blockTemplatePartData)
        {
            return DoPutAsync<JsonDocument>("blocktemplatepart", blockTemplatePartData).Result;
        }

        //   DELETE /api/external/boromir/blocktemplatepart?id={blockTemplatePartId}
        public bool DeleteBlockTemplatePart(string blockTemplatePartId)
        {
            return DoDeleteAsync<bool>("blocktemplatepart", blockTemplatePartId).Result;
        }

        //   PATCH /api/external/boromir/blocktemplatepart?id={blockTemplatePartId}
        public JsonDocument UpdateBlockTemplatePart(string blockTemplatePartId, JsonDocument blockTemplatePartData)
        {
            return DoPatchAsync<JsonDocument>("blocktemplatepart", blockTemplatePartId, blockTemplatePartData).Result;
        }

        //   POST /api/external/boromir/blocktemplate/blocktemplateparts?id={blockTemplateId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListBlockTemplatePartsForBlockTemplate(string blockTemplateId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("blocktemplate/blocktemplateparts", blockTemplateId, limit, offset).Result;
        }

        //==============================================================================================//
        // CATEGORY METHODS

        //   GET /api/external/boromir/category?id={categoryId}&granularity={granularity}
        public JsonDocument GetCategoryById(string categoryId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(categoryId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 1 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("category", categoryId, granularity).Result;
        }

        public WorldAnvilCategory? GetWorldAnvilCategoryObjectById(string categoryId, int granularity)
        {
            try
            {
                JsonDocument? result = GetCategoryById(categoryId, granularity) ?? throw new Exception("GetWorldById returned null");

                // serialize the JSON into the WorldAnvilUser object
                WorldAnvilCategory? worldAnvilCategory = JsonDocumentExtensions.ToObject<WorldAnvilCategory>(result, JsonDocumentExtensions.JsonConversionOptions);

                if (worldAnvilCategory != null)
                {
                    worldAnvilCategory.granularity = granularity;
                }

                return worldAnvilCategory;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        //   PUT /api/external/boromir/category
        public JsonDocument CreateCategory(JsonDocument categoryData)
        {
            return DoPutAsync<JsonDocument>("category", categoryData).Result;
        }

        //   DELETE /api/external/boromir/category?id={categoryId}
        public bool DeleteCategory(string categoryId)
        {
            ArgumentException.ThrowIfNullOrEmpty(categoryId);
            return DoDeleteAsync<bool>("category", categoryId).Result;
        }

        //   PATCH /api/external/boromir/category?id={categoryId}
        public JsonDocument UpdateCategory(string categoryId, JsonDocument categoryData)
        {
            ArgumentException.ThrowIfNullOrEmpty(categoryId);
            return DoPatchAsync<JsonDocument>("category", categoryId, categoryData).Result;
        }

        //   POST /api/external/boromir/world/categories?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListCategoriesForWorld(string worldId, int limit, int offset)
        {
            ArgumentException.ThrowIfNullOrEmpty(worldId);
            return DoPostAsync<List<JsonDocument>>("world/categories", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // CANVAS METHODS

        //   GET /api/external/boromir/canvas?id={canvasId}&granularity={granularity}
        public JsonDocument GetCanvasById(string canvasId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(canvasId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("canvas", canvasId, granularity).Result;
        }

        //   PUT /api/external/boromir/canvas
        public JsonDocument CreateCanvas(JsonDocument canvasData)
        {
            return DoPutAsync<JsonDocument>("canvas", canvasData).Result;
        }

        //   DELETE /api/external/boromir/canvas?id={canvasId}
        public bool DeleteCanvas(string canvasId)
        {
            ArgumentException.ThrowIfNullOrEmpty(canvasId);
            return DoDeleteAsync<bool>("canvas", canvasId).Result;
        }

        //   PATCH /api/external/boromir/canvas?id={canvasId}
        public JsonDocument UpdateCanvas(string canvasId, JsonDocument canvasData)
        {
            ArgumentException.ThrowIfNullOrEmpty(canvasId);
            return DoPatchAsync<JsonDocument>("canvas", canvasId, canvasData).Result;
        }

        //   POST /api/external/boromir/world/canvases?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListCanvasesForWorld(string worldId, int limit, int offset)
        {
            ArgumentException.ThrowIfNullOrEmpty(worldId);
            return DoPostAsync<List<JsonDocument>>("world/canvases", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // RPG SYSTEM METHODS

        //   GET /api/external/boromir/rpgsystem?id={rpgSystemId}&granularity={granularity}
        public JsonDocument GetRpgSystemById(string rpgSystemId, int granularity)
        {
            return DoGetAsync("rpgsystem", rpgSystemId, granularity).Result;
        }

        //   POST /api/external/boromir/rpgsystems
        public List<JsonDocument> ListRpgSystems()
        {
            return DoPostAsync<List<JsonDocument>>("rpgsystems").Result;
        }

        //==============================================================================================//
        // TIMELINE METHODS

        //   GET /api/external/boromir/timeline?id={timelineId}&granularity={granularity}
        public JsonDocument GetTimelineById(string timelineId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(timelineId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 1 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("timeline", timelineId, granularity).Result;
        }

        //   PUT /api/external/boromir/timeline
        public JsonDocument CreateTimeline(JsonDocument timelineData)
        {
            return DoPutAsync<JsonDocument>("timeline", timelineData).Result;
        }

        //   DELETE /api/external/boromir/timeline?id={timelineId}
        public bool DeleteTimeline(string timelineId)
        {
            ArgumentException.ThrowIfNullOrEmpty(timelineId);
            return DoDeleteAsync<bool>("timeline", timelineId).Result;
        }

        //   PATCH /api/external/boromir/timeline?id={timelineId}
        public JsonDocument UpdateTimeline(string timelineId, JsonDocument timelineData)
        {
            ArgumentException.ThrowIfNullOrEmpty(timelineId);
            return DoPatchAsync<JsonDocument>("timeline", timelineId, timelineData).Result;
        }

        //   POST /api/external/boromir/world/timelines?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListTimelinesForWorld(string worldId, int limit, int offset)
        {
            ArgumentException.ThrowIfNullOrEmpty(worldId);
            return DoPostAsync<List<JsonDocument>>("world/timelines", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // HISTORY METHODS

        //   GET /api/external/boromir/history?id={historyId}&granularity={granularity}
        public JsonDocument GetHistoryById(string historyId, int granularity)
        {
            return DoGetAsync("history", historyId, granularity).Result;
        }

        //   PUT /api/external/boromir/history
        public JsonDocument CreateHistory(JsonDocument historyData)
        {
            return DoPutAsync<JsonDocument>("history", historyData).Result;
        }

        //   DELETE /api/external/boromir/history?id={historyId}
        public bool DeleteHistory(string historyId)
        {
            return DoDeleteAsync<bool>("history", historyId).Result;
        }

        //   PATCH /api/external/boromir/history?id={historyId}
        public JsonDocument UpdateHistory(string historyId, JsonDocument historyData)
        {
            return DoPatchAsync<JsonDocument>("history", historyId, historyData).Result;
        }

        //   POST /api/external/boromir/world/histories?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListHistoriesForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/histories", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // IMAGE METHODS

        //   GET /api/external/boromir/image?id={imageId}&granularity={granularity}
        public JsonDocument GetImageMetadataById(string imageId, int granularity)
        {
            return DoGetAsync("image", imageId, granularity).Result;
        }

        //   PUT /api/external/boromir/image
        public JsonDocument CreateImage(JsonDocument imageData)
        {
            throw new NotImplementedException();
        }

        //   DELETE /api/external/boromir/image?id={imageId}
        public bool DeleteImage(string imageId)
        {
            return DoDeleteAsync<bool>("image", imageId).Result;
        }

        //   PATCH /api/external/boromir/image?id={imageId}
        public JsonDocument UpdateImageMetadata(string imageId, JsonDocument imageData)
        {
            return DoPatchAsync<JsonDocument>("image", imageId, imageData).Result;
        }

        //   POST /api/external/boromir/world/images?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListImagesForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/images", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // MAP METHODS

        //   GET /api/external/boromir/map?id={mapId}&granularity={granularity}
        public JsonDocument GetMapById(string mapId, int granularity)
        {
            return DoGetAsync("map", mapId, granularity).Result;
        }

        //   PUT /api/external/boromir/map
        public JsonDocument CreateMap(JsonDocument mapData)
        {
            return DoPutAsync<JsonDocument>("map", mapData).Result;
        }

        //   DELETE /api/external/boromir/map?id={mapId}
        public bool DeleteMap(string mapId)
        {
            return DoDeleteAsync<bool>("map", mapId).Result;
        }

        //   PATCH /api/external/boromir/map?id={mapId}
        public JsonDocument UpdateMap(string mapId, JsonDocument mapData)
        {
            return DoPatchAsync<JsonDocument>("map", mapId, mapData).Result;
        }

        //   POST /api/external/boromir/world/maps?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListMapsForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/maps", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // MAP LAYER METHODS

        //   GET /api/external/boromir/layer?id={mapLayerId}&granularity={granularity}
        public JsonDocument GetMapLayerById(string mapLayerId, int granularity)
        {
            return DoGetAsync("layer", mapLayerId, granularity).Result;
        }

        //   PUT /api/external/boromir/layer
        public JsonDocument CreateMapLayer(JsonDocument mapLayerData)
        {
            return DoPutAsync<JsonDocument>("layer", mapLayerData).Result;
        }

        //   DELETE /api/external/boromir/layer?id={mapLayerId}
        public bool DeleteMapLayer(string mapLayerId)
        {
            return DoDeleteAsync<bool>("layer", mapLayerId).Result;
        }

        //   PATCH /api/external/boromir/layer?id={mapLayerId}
        public JsonDocument UpdateMapLayer(string mapLayerId, JsonDocument mapLayerData)
        {
            return DoPatchAsync<JsonDocument>("layer", mapLayerId, mapLayerData).Result;
        }

        //   POST /api/external/boromir/map/layers?id={mapId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListMapLayersForMap(string mapId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("map/layers", mapId, limit, offset).Result;
        }

        //==============================================================================================//
        // MARKER GROUP METHODS

        //   GET /api/external/boromir/markergroup?id={markerGroupId}&granularity={granularity}
        public JsonDocument GetMarkerGroupById(string markerGroupId, int granularity)
        {
            return DoGetAsync("markergroup", markerGroupId, granularity).Result;
        }

        //   PUT /api/external/boromir/markergroup
        public JsonDocument CreateMarkerGroup(JsonDocument markerGroupData)
        {
            return DoPutAsync<JsonDocument>("markergroup", markerGroupData).Result;
        }

        //   DELETE /api/external/boromir/markergroup?id={markerGroupId}
        public bool DeleteMarkerGroup(string markerGroupId)
        {
            return DoDeleteAsync<bool>("markergroup", markerGroupId).Result;
        }

        //   PATCH /api/external/boromir/markergroup?id={markerGroupId}
        public JsonDocument UpdateMarkerGroup(string markerGroupId, JsonDocument markerGroupData)
        {
            return DoPatchAsync<JsonDocument>("markergroup", markerGroupId, markerGroupData).Result;
        }

        //   POST /api/external/boromir/map/markergroups?id={mapId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListMarkerGroupsForMap(string mapId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("map/markergroups", mapId, limit, offset).Result;
        }

        //==============================================================================================//
        // MARKER METHODS

        //   GET /api/external/boromir/marker?id={markerId}&granularity={granularity}
        public JsonDocument? GetMarkerById(string markerId, int granularity)
        {
            return DoGetAsync("marker", markerId, granularity).Result;
        }

        //   PUT /api/external/boromir/marker
        public JsonDocument? CreateMarker(JsonDocument markerData)
        {
            return DoPutAsync<JsonDocument>("marker", markerData).Result;
        }

        //   DELETE /api/external/boromir/marker?id={markerId}
        public bool DeleteMarker(string markerId)
        {
            return DoDeleteAsync<bool>("marker", markerId).Result;
        }

        //   PATCH /api/external/boromir/marker?id={markerId}
        public JsonDocument UpdateMarker(string markerId, JsonDocument markerData)
        {
            return DoPatchAsync<JsonDocument>("marker", markerId, markerData).Result;
        }

        //   POST /api/external/boromir/markergroup/markers?id={markerGroupId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListMarkersForMarkerGroup(string markerGroupId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("markergroup/markers", markerGroupId, limit, offset).Result;
        }

        //==============================================================================================//
        // MARKER TYPE METHODS

        //   GET /api/external/boromir/markertype?id={markerTypeId}&granularity={granularity}
        public JsonDocument? GetMarkerTypeById(string markerTypeId, int granularity)
        {
            return DoGetAsync("markertype", markerTypeId, granularity).Result;
        }

        //   PUT /api/external/boromir/markertype
        public JsonDocument? CreateMarkerType(JsonDocument markerTypeData)
        {
            return DoPutAsync<JsonDocument>("markertype", markerTypeData).Result;
        }

        //   DELETE /api/external/boromir/markertype?id={markerTypeId}
        public bool DeleteMarkerType(string markerTypeId)
        {
            return DoDeleteAsync<bool>("markertype", markerTypeId).Result;
        }

        //   PATCH /api/external/boromir/markertype?id={markerTypeId}
        public JsonDocument UpdateMarkerType(string markerTypeId, JsonDocument markerTypeData)
        {
            return DoPatchAsync<JsonDocument>("markertype", markerTypeId, markerTypeData).Result;
        }

        //   POST /api/external/boromir/markertypes?limit={limit}&offset={offset}
        public List<JsonDocument> ListMarkerTypes(int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("markertypes", "", limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT METHODS

        //   GET /api/external/boromir/manuscript?id={manuscriptId}&granularity={granularity}
        public JsonDocument? GetManuscriptById(string manuscriptId, int granularity)
        {
            return DoGetAsync("manuscript", manuscriptId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript
        public JsonDocument? CreateManuscript(JsonDocument manuscriptData)
        {
            return DoPutAsync<JsonDocument>("manuscript", manuscriptData).Result;
        }

        //   DELETE /api/external/boromir/manuscript?id={manuscriptId}
        public bool DeleteManuscript(string manuscriptId)
        {
            return DoDeleteAsync<bool>("manuscript", manuscriptId).Result;
        }

        //   PATCH /api/external/boromir/manuscript?id={manuscriptId}
        public JsonDocument UpdateManuscript(string manuscriptId, JsonDocument manuscriptData)
        {
            return DoPatchAsync<JsonDocument>("manuscript", manuscriptId, manuscriptData).Result;
        }

        //   POST /api/external/boromir/world/manuscripts?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptsForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/manuscripts", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT BEAT METHODS

        //   GET /api/external/boromir/manuscript_beat?id={manuscriptBeatId}&granularity={granularity}
        public JsonDocument? GetManuscriptBeatById(string manuscriptBeatId, int granularity)
        {
            return DoGetAsync("manuscript_beat", manuscriptBeatId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_beat
        public JsonDocument? CreateManuscriptBeat(JsonDocument manuscriptBeatData)
        {
            return DoPutAsync<JsonDocument>("manuscript_beat", manuscriptBeatData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_beat?id={manuscriptBeatId}
        public bool DeleteManuscriptBeat(string manuscriptBeatId)
        {
            return DoDeleteAsync<bool>("manuscript_beat", manuscriptBeatId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_beat?id={manuscriptBeatId}
        public JsonDocument UpdateManuscriptBeat(string manuscriptBeatId, JsonDocument manuscriptBeatData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_beat", manuscriptBeatId, manuscriptBeatData).Result;
        }

        //   POST /api/external/boromir/manuscript_part/manuscript_beats?id={manuscriptPartId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptBeatsForManuscriptPart(string manuscriptPartId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript_part/manuscript_beats", manuscriptPartId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT BOOKMARK METHODS

        //   GET /api/external/boromir/manuscript_bookmark?id={manuscriptBookmarkId}&granularity={granularity}
        public JsonDocument? GetManuscriptBookmarkById(string manuscriptBookmarkId, int granularity)
        {
            return DoGetAsync("manuscript_bookmark", manuscriptBookmarkId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_bookmark
        public JsonDocument? CreateManuscriptBookmark(JsonDocument manuscriptBookmarkData)
        {
            return DoPutAsync<JsonDocument>("manuscript_bookmark", manuscriptBookmarkData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_bookmark?id={manuscriptBookmarkId}
        public bool DeleteManuscriptBookmark(string manuscriptBookmarkId)
        {
            return DoDeleteAsync<bool>("manuscript_bookmark", manuscriptBookmarkId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_bookmark?id={manuscriptBookmarkId}
        public JsonDocument UpdateManuscriptBookmark(string manuscriptBookmarkId, JsonDocument manuscriptBookmarkData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_bookmark", manuscriptBookmarkId, manuscriptBookmarkData).Result;
        }

        //   POST /api/external/boromir/manuscript/manuscript_bookmarks?id={manuscriptId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptBookmarksForManuscript(string manuscriptId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript/manuscript_bookmarks", manuscriptId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT PART METHODS

        //   GET /api/external/boromir/manuscript_part?id={manuscriptPartId}&granularity={granularity}
        public JsonDocument? GetManuscriptPartById(string manuscriptPartId, int granularity)
        {
            return DoGetAsync("manuscript_part", manuscriptPartId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_part
        public JsonDocument? CreateManuscriptPart(JsonDocument manuscriptPartData)
        {
            return DoPutAsync<JsonDocument>("manuscript_part", manuscriptPartData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_part?id={manuscriptPartId}
        public bool DeleteManuscriptPart(string manuscriptPartId)
        {
            return DoDeleteAsync<bool>("manuscript_part", manuscriptPartId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_part?id={manuscriptPartId}
        public JsonDocument UpdateManuscriptPart(string manuscriptPartId, JsonDocument manuscriptPartData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_part", manuscriptPartId, manuscriptPartData).Result;
        }

        //   POST /api/external/boromir/manuscript_version/manuscript_parts?id={manuscriptVersionId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptPartsForManuscriptVersion(string manuscriptVersionId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript_version/manuscript_parts", manuscriptVersionId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT TAG METHODS

        //   GET /api/external/boromir/manuscript_tag?id={manuscriptTagId}&granularity={granularity}
        public JsonDocument? GetManuscriptTagById(string manuscriptTagId, int granularity)
        {
            return DoGetAsync("manuscript_tag", manuscriptTagId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_tag
        public JsonDocument? CreateManuscriptTag(JsonDocument manuscriptTagData)
        {
            return DoPutAsync<JsonDocument>("manuscript_tag", manuscriptTagData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_tag?id={manuscriptTagId}
        public bool DeleteManuscriptTag(string manuscriptTagId)
        {
            return DoDeleteAsync<bool>("manuscript_tag", manuscriptTagId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_tag?id={manuscriptTagId}
        public JsonDocument UpdateManuscriptTag(string manuscriptTagId, JsonDocument manuscriptTagData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_tag", manuscriptTagId, manuscriptTagData).Result;
        }

        //   POST /api/external/boromir/manuscript/manuscript_tags?id={manuscriptId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptTagsForManuscript(string manuscriptId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript/manuscript_tags", manuscriptId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT STAT METHODS

        //   GET /api/external/boromir/manuscript_stat?id={manuscriptStatId}&granularity={granularity}
        public JsonDocument? GetManuscriptStatById(string manuscriptStatId, int granularity)
        {
            return DoGetAsync("manuscript_stat", manuscriptStatId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_stat
        public JsonDocument? CreateManuscriptStat(JsonDocument manuscriptStatData)
        {
            return DoPutAsync<JsonDocument>("manuscript_stat", manuscriptStatData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_stat?id={manuscriptStatId}
        public bool DeleteManuscriptStat(string manuscriptStatId)
        {
            return DoDeleteAsync<bool>("manuscript_stat", manuscriptStatId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_stat?id={manuscriptStatId}
        public JsonDocument UpdateManuscriptStat(string manuscriptStatId, JsonDocument manuscriptStatData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_stat", manuscriptStatId, manuscriptStatData).Result;
        }

        //   POST /api/external/boromir/manuscript_version/manuscript_stats?id={manuscriptVersionId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptStatsForManuscriptVersion(string manuscriptVersionId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript_version/manuscript_stats", manuscriptVersionId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT LABEL METHODS

        //   GET /api/external/boromir/manuscript_label?id={manuscriptLabelId}&granularity={granularity}
        public JsonDocument? GetManuscriptLabelById(string manuscriptLabelId, int granularity)
        {
            return DoGetAsync("manuscript_label", manuscriptLabelId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_label
        public JsonDocument? CreateManuscriptLabel(JsonDocument manuscriptLabelData)
        {
            return DoPutAsync<JsonDocument>("manuscript_label", manuscriptLabelData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_label?id={manuscriptLabelId}
        public bool DeleteManuscriptLabel(string manuscriptLabelId)
        {
            return DoDeleteAsync<bool>("manuscript_label", manuscriptLabelId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_label?id={manuscriptLabelId}
        public JsonDocument UpdateManuscriptLabel(string manuscriptLabelId, JsonDocument manuscriptLabelData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_label", manuscriptLabelId, manuscriptLabelData).Result;
        }

        //   POST /api/external/boromir/manuscript/manuscript_labels?id={manuscriptId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptLabelsForManuscript(string manuscriptId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript/manuscript_labels", manuscriptId, limit, offset).Result;
        }

        //==============================================================================================//
        // MANUSCRIPT PLOT METHODS

        //   GET /api/external/boromir/manuscript_plot?id={manuscriptPlotId}&granularity={granularity}
        public JsonDocument? GetManuscriptPlotById(string manuscriptPlotId, int granularity)
        {
            return DoGetAsync("manuscript_plot", manuscriptPlotId, granularity).Result;
        }

        //   PUT /api/external/boromir/manuscript_plot
        public JsonDocument? CreateManuscriptPlot(JsonDocument manuscriptPlotData)
        {
            return DoPutAsync<JsonDocument>("manuscript_plot", manuscriptPlotData).Result;
        }

        //   DELETE /api/external/boromir/manuscript_plot?id={manuscriptPlotId}
        public bool DeleteManuscriptPlot(string manuscriptPlotId)
        {
            return DoDeleteAsync<bool>("manuscript_plot", manuscriptPlotId).Result;
        }

        //   PATCH /api/external/boromir/manuscript_plot?id={manuscriptPlotId}
        public JsonDocument UpdateManuscriptPlot(string manuscriptPlotId, JsonDocument manuscriptPlotData)
        {
            return DoPatchAsync<JsonDocument>("manuscript_plot", manuscriptPlotId, manuscriptPlotData).Result;
        }

        //   POST /api/external/boromir/manuscript_version/manuscript_plots?id={manuscriptVersionId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListManuscriptPlotsForManuscriptVersion(string manuscriptVersionId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("manuscript_version/manuscript_plots", manuscriptVersionId, limit, offset).Result;
        }

        //==============================================================================================//
        // NOTEBOOK METHODS

        //   GET /api/external/boromir/notebook?id={notebookId}&granularity={granularity}
        public JsonDocument? GetNotebookById(string notebookId, int granularity)
        {
            return DoGetAsync("notebook", notebookId, granularity).Result;
        }

        //   PUT /api/external/boromir/notebook
        public JsonDocument? CreateNotebook(JsonDocument notebookData)
        {
            return DoPutAsync<JsonDocument>("notebook", notebookData).Result;
        }

        //   DELETE /api/external/boromir/notebook?id={notebookId}
        public bool DeleteNotebook(string notebookId)
        {
            return DoDeleteAsync<bool>("notebook", notebookId).Result;
        }

        //   PATCH /api/external/boromir/notebook?id={notebookId}
        public JsonDocument UpdateNotebook(string notebookId, JsonDocument notebookData)
        {
            return DoPatchAsync<JsonDocument>("notebook", notebookId, notebookData).Result;
        }

        //   POST /api/external/boromir/world/notebooks?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListNotebooksForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/notebooks", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // NOTESECTION METHODS

        //   GET /api/external/boromir/notesection?id={notesectionId}&granularity={granularity}
        public JsonDocument? GetNoteSectionById(string notesectionId, int granularity)
        {
            return DoGetAsync("notesection", notesectionId, granularity).Result;
        }

        //   PUT /api/external/boromir/notesection
        public JsonDocument? CreateNoteSection(JsonDocument notesectionData)
        {
            return DoPutAsync<JsonDocument>("notesection", notesectionData).Result;
        }

        //   DELETE /api/external/boromir/notesection?id={notesectionId}
        public bool DeleteNoteSection(string notesectionId)
        {
            return DoDeleteAsync<bool>("notesection", notesectionId).Result;
        }

        //   PATCH /api/external/boromir/notesection?id={notesectionId}
        public JsonDocument UpdateNoteSection(string notesectionId, JsonDocument notesectionData)
        {
            return DoPatchAsync<JsonDocument>("notesection", notesectionId, notesectionData).Result;
        }

        //   POST /api/external/boromir/notebook/notesections?id={notebookId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListNoteSectionsForNotebook(string notebookId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("notebook/notesections", notebookId, limit, offset).Result;
        }

        //==============================================================================================//
        // NOTE METHODS

        //   GET /api/external/boromir/note?id={noteId}&granularity={granularity}
        public JsonDocument? GetNoteById(string noteId, int granularity)
        {
            return DoGetAsync("note", noteId, granularity).Result;
        }

        //   PUT /api/external/boromir/note
        public JsonDocument? CreateNote(JsonDocument noteData)
        {
            return DoPutAsync<JsonDocument>("note", noteData).Result;
        }

        //   DELETE /api/external/boromir/note?id={noteId}
        public bool DeleteNote(string noteId)
        {
            return DoDeleteAsync<bool>("note", noteId).Result;
        }

        //   PATCH /api/external/boromir/note?id={noteId}
        public JsonDocument UpdateNote(string noteId, JsonDocument noteData)
        {
            return DoPatchAsync<JsonDocument>("note", noteId, noteData).Result;
        }

        //   POST /api/external/boromir/notesection/notes?id={notesectionId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListNotesForNoteSection(string notesectionId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("notesection/notes", notesectionId, limit, offset).Result;
        }

        //==============================================================================================//
        // SECRET METHODS

        //   GET /api/external/boromir/secret?id={secretId}&granularity={granularity}
        public JsonDocument? GetSecretById(string secretId, int granularity)
        {
            return DoGetAsync("secret", secretId, granularity).Result;
        }

        //   PUT /api/external/boromir/secret
        public JsonDocument? CreateSecret(JsonDocument secretData)
        {
            return DoPutAsync<JsonDocument>("secret", secretData).Result;
        }

        //   DELETE /api/external/boromir/secret?id={secretId}
        public bool DeleteSecret(string secretId)
        {
            return DoDeleteAsync<bool>("secret", secretId).Result;
        }

        //   PATCH /api/external/boromir/secret?id={secretId}
        public JsonDocument UpdateSecret(string secretId, JsonDocument secretData)
        {
            return DoPatchAsync<JsonDocument>("secret", secretId, secretData).Result;
        }

        //   POST /api/external/boromir/world/secrets?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListSecretsForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/secrets", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // SUBSCRIBERGROUP METHODS

        //   GET /api/external/boromir/subscribergroup?id={subscriberGroupId}&granularity={granularity}
        public JsonDocument? GetSubscriberGroupById(string subscriberGroupId, int granularity)
        {
            return DoGetAsync("subscribergroup", subscriberGroupId, granularity).Result;
        }

        //   PUT /api/external/boromir/subscribergroup
        public JsonDocument? CreateSubscriberGroup(JsonDocument subscriberGroupData)
        {
            return DoPutAsync<JsonDocument>("subscribergroup", subscriberGroupData).Result;
        }

        //   DELETE /api/external/boromir/subscribergroup?id={subscriberGroupId}
        public bool DeleteSubscriberGroup(string subscriberGroupId)
        {
            return DoDeleteAsync<bool>("subscribergroup", subscriberGroupId).Result;
        }

        //   PATCH /api/external/boromir/subscribergroup?id={subscriberGroupId}
        public JsonDocument UpdateSubscriberGroup(string subscriberGroupId, JsonDocument subscriberGroupData)
        {
            return DoPatchAsync<JsonDocument>("subscribergroup", subscriberGroupId, subscriberGroupData).Result;
        }

        //   POST /api/external/boromir/world/subscribergroups?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListSubscriberGroupsForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/subscribergroups", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // VARIABLE METHODS

        //   GET /api/external/boromir/variable?id={variableId}&granularity={granularity}
        public JsonDocument? GetVariableById(string variableId, int granularity)
        {
            return DoGetAsync("variable", variableId, granularity).Result;
        }

        //   PUT /api/external/boromir/variable
        public JsonDocument? CreateVariable(JsonDocument variableData)
        {
            return DoPutAsync<JsonDocument>("variable", variableData).Result;
        }

        //   DELETE /api/external/boromir/variable?id={variableId}
        public bool DeleteVariable(string variableId)
        {
            return DoDeleteAsync<bool>("variable", variableId).Result;
        }

        //   PATCH /api/external/boromir/variable?id={variableId}
        public JsonDocument UpdateVariable(string variableId, JsonDocument variableData)
        {
            return DoPatchAsync<JsonDocument>("variable", variableId, variableData).Result;
        }

        //   POST /api/external/boromir/variable_collection/variables?id={variableCollectionId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListVariablesForVariableCollection(string variableCollectionId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("variable_collection/variables", variableCollectionId, limit, offset).Result;
        }

        //==============================================================================================//
        // VARIABLE COLLECTION METHODS

        //   GET /api/external/boromir/variable_collection?id={variableCollectionId}&granularity={granularity}
        public JsonDocument? GetVariableCollectionById(string variableCollectionId, int granularity)
        {
            return DoGetAsync("variable_collection", variableCollectionId, granularity).Result;
        }

        //   PUT /api/external/boromir/variable_collection
        public JsonDocument? CreateVariableCollection(JsonDocument variableCollectionData)
        {
            return DoPutAsync<JsonDocument>("variable_collection", variableCollectionData).Result;
        }

        //   DELETE /api/external/boromir/variable_collection?id={variableCollectionId}
        public bool DeleteVariableCollection(string variableCollectionId)
        {
            return DoDeleteAsync<bool>("variable_collection", variableCollectionId).Result;
        }

        //   PATCH /api/external/boromir/variable_collection?id={variableCollectionId}
        public JsonDocument UpdateVariableCollection(string variableCollectionId, JsonDocument variableCollectionData)
        {
            return DoPatchAsync<JsonDocument>("variable_collection", variableCollectionId, variableCollectionData).Result;
        }

        //   POST /api/external/boromir/world/variable_collections?id={worldId}&limit={limit}&offset={offset}
        public List<JsonDocument> ListVariableCollectionsForWorld(string worldId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("world/variable_collections", worldId, limit, offset).Result;
        }

        //==============================================================================================//
        // USER METHODS

        //   GET /api/external/boromir/user?id={userId}&granularity={granularity}
        public JsonDocument? GetUserById(string userId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 2, true, nameof(granularity));

            return DoGetAsync("user", userId, granularity).Result;
        }

        public WorldAnvilUser? GetWorldAnvilUserObjectById(string userId, int granularity)
        {
            try
            {
                JsonDocument? result = GetUserById(userId, granularity) ?? throw new Exception("GetUserById returned null");

                // serialize the JSON into the WorldAnvilUser object
                WorldAnvilUser? worldAnvilUser = JsonDocumentExtensions.ToObject<WorldAnvilUser>(result, JsonDocumentExtensions.JsonConversionOptions);

                if (worldAnvilUser != null)
                {
                    worldAnvilUser.granularity = granularity;
                }

                return worldAnvilUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        //   PATCH /api/external/boromir/user?id={userId}
        public JsonDocument UpdateUser(string userId, JsonDocument userData)
        {
            return DoPatchAsync<JsonDocument>("user", userId, userData).Result;
        }

        //   GET /api/external/boromir/identity
        public string? GetUserIdentity()
        {
            return GetUserIdentityAsync<string>().GetAwaiter().GetResult();
        }

        //==============================================================================================//
        // WORLD METHODS

        //   GET /api/external/boromir/world?id={worldId}&granularity={granularity}
        public JsonDocument? GetWorldById(string worldId, int granularity)
        {
            ArgumentException.ThrowIfNullOrEmpty(worldId);
            ArgumentOutOfRangeException.ThrowIfNotEqual(granularity == -1 || granularity == 0 || granularity == 1, true, nameof(granularity));

            return DoGetAsync("world", worldId, granularity).Result;
        }

        public WorldAnvilWorld? GetWorldAnvilWorldObjectById(string worldId, int granularity)
        {
            try
            {
                JsonDocument? result = GetWorldById(worldId, granularity) ?? throw new Exception("GetWorldById returned null");

                // serialize the JSON into the WorldAnvilUser object
                WorldAnvilWorld? worldAnvilWorld = JsonDocumentExtensions.ToObject<WorldAnvilWorld>(result, JsonDocumentExtensions.JsonConversionOptions);

                if (worldAnvilWorld != null)
                {
                    worldAnvilWorld.granularity = granularity;
                }

                return worldAnvilWorld;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        //   PUT /api/external/boromir/world
        public JsonDocument? CreateWorld(JsonDocument worldData)
        {
            return DoPutAsync<JsonDocument>("world", worldData).Result;
        }

        //   DELETE /api/external/boromir/world?id={worldId}
        public bool DeleteWorld(string worldId)
        {
            return DoDeleteAsync<bool>("world", worldId).Result;
        }

        //   PATCH /api/external/boromir/world?id={worldId}
        public JsonDocument? UpdateWorld(string worldId, JsonDocument worldData)
        {
            return DoPatchAsync<JsonDocument>("world", worldId, worldData).Result;
        }

        //   POST /api/external/boromir/user/worlds?limit={limit}&offset={offset}
        public List<JsonDocument> ListWorldsForUser(string userId, int limit, int offset)
        {
            return DoPostAsync<List<JsonDocument>>("user/worlds", userId, limit, offset).Result;
        }


        //==============================================================================================//
        // Private Methods
        //==============================================================================================//
        // Retrieves the World Anvil API Key from the brookmonte.com endpoint using the provided JWT token
        // TODO: since this is specific to Realm Studio, move it to another namespace and class
        private async Task<string?> GetWAApiKey(string token)
        {
            // this environment variable is set by the Realm Studio installer
            string? apiEndpoint = Environment.GetEnvironmentVariable("BKMT_WA_SECURE_DATA");

            if (!string.IsNullOrWhiteSpace(apiEndpoint))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;

                return root.GetProperty("message").GetString();
            }

            return null;
        }

        // Retrieves the JWT token from the brookmonte.com endpoint using installer-set environment variables
        // TODO: since this is specific to Realm Studio, move it to another namespace and class
        private async Task<string?> GetJwtTokenAsync()
        {
            // these environment variables are set by the Realm Studio installer
            string? tokenEndpoint = Environment.GetEnvironmentVariable("BKMT_WP_API_TOKEN_ENDPOINT");
            string? tokenUid = Environment.GetEnvironmentVariable("BKMT_WP_API_TOKEN_UID");
            string? tokenPw = Environment.GetEnvironmentVariable("BKMT_WP_API_TOKEN_PW");

            if (!string.IsNullOrWhiteSpace(tokenEndpoint) && !string.IsNullOrWhiteSpace(tokenUid) && !string.IsNullOrWhiteSpace(tokenPw))
            {
                var requestData = new
                {
                    username = tokenUid,
                    password = tokenPw
                };

                var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
                };

                var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                // Read the response content
                JsonDocument responseData = await response.Content.ReadAsStringAsync()
                    is string jsonString
                    ? JsonDocument.Parse(jsonString)
                    : throw new InvalidOperationException("Failed to read response content.");

                response.Dispose();

                // get the JWT token from the responseData
                JsonElement root = responseData.RootElement;

                Dictionary<string, string> flatJson = FlattenJson(root);

                flatJson.TryGetValue("/token", out string? token);

                return token;
            }

            return null;
        }

        //==============================================================================================//
        // Private World Anvil API Methods
        //==============================================================================================//

        //   Get user identity
        //   GET /api/external/boromir/identity
        private async Task<string?> GetUserIdentityAsync<T>()
        {
            string endpoint = $"{WaApiBaseUrl}identity";

            // the WorldAnvilUserApiToken is the user token created in World Anvil
            // see https://www.worldanvil.com/api/auth/key when logged in to World Anvil
            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Get, WorldAnvilUserApiToken, WorldAnvilAPIKey);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}identity";
                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Get, WorldAnvilUserApiToken, WorldAnvilAPIKey);

                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            // Read the response content
            JsonDocument responseData = await response.Content.ReadAsStringAsync()
                is string jsonString
                ? JsonDocument.Parse(jsonString)
                : throw new InvalidOperationException("Failed to read response content.");

            response.Dispose();

            // get the user identity from the responseData
            JsonElement root = responseData.RootElement;

            Dictionary<string, string> flatJson = FlattenJson(root);

            flatJson.TryGetValue("/id", out string? userId);

            return userId;
        }

        #region UTILITY METHODS
        //==============================================================================================//
        //==============================================================================================//
        // Utility Methods

        //  Generic GET method for World Anvil API
        private async Task<JsonDocument> DoGetAsync(string url, string id, int granularity)
        {
            string endpoint = $"{WaApiBaseUrl}{url}?id={id}&granularity={granularity}";

            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Get, WorldAnvilUserApiToken, WorldAnvilAPIKey);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}{url}?id={id}&granularity={granularity}";
                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Get, WorldAnvilUserApiToken, WorldAnvilAPIKey);

                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            JsonDocument responseData = await response.Content.ReadAsStringAsync()
                is string jsonString
                ? JsonDocument.Parse(jsonString)
                : throw new InvalidOperationException("Failed to read response content.");

            response.Dispose();

            return responseData;
        }

        //  Generic PUT method for World Anvil API
        private async Task<JsonDocument> DoPutAsync<T>(string url, JsonDocument jsonData)
        {
            string endpoint = $"{WaApiBaseUrl}{url}";

            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Put, WorldAnvilUserApiToken, WorldAnvilAPIKey, jsonData);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
                || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}{url}";

                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Put, WorldAnvilUserApiToken, WorldAnvilAPIKey, jsonData);

                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            // Read the response content
            JsonDocument responseData = await response.Content.ReadAsStringAsync()
                is string jsonString
                ? JsonDocument.Parse(jsonString)
                : throw new InvalidOperationException("Failed to read response content.");

            response.Dispose();

            return responseData;
        }

        // Generic DELETE method for World Anvil API
        private async Task<bool> DoDeleteAsync<T>(string url, string id)
        {
            string endpoint = $"{WaApiBaseUrl}{url}?id={id}";

            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Delete, WorldAnvilUserApiToken, WorldAnvilAPIKey);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}{url}?id={id}";

                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Delete, WorldAnvilUserApiToken, WorldAnvilAPIKey);

                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            response.Dispose();

            return true;
        }

        // Generic PATCH method for World Anvil API
        private async Task<JsonDocument> DoPatchAsync<T>(string url, string id, JsonDocument jsonData)
        {
            string endpoint = $"{WaApiBaseUrl}{url}?id={id}";

            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Patch, WorldAnvilUserApiToken, WorldAnvilAPIKey, jsonData);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}{url}?id={id}";

                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Patch, WorldAnvilUserApiToken, WorldAnvilAPIKey, jsonData);

                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            // Read the response content
            JsonDocument responseData = await response.Content.ReadAsStringAsync()
                is string jsonString
                ? JsonDocument.Parse(jsonString)
                : throw new InvalidOperationException("Failed to read response content.");

            response.Dispose();

            return responseData;
        }

        private async Task<List<JsonDocument>> DoPostAsync<T>(string url, string id = "", int limit = 0, int offset = 0, string? category = null)
        {
            string endpoint = $"{WaApiBaseUrl}{url}";

            if (!string.IsNullOrEmpty(id))
            {
                endpoint += $"?id={id}";
            }

            string contentString = "{ \"limit\": \"" + limit + "\", "
                + "\"offset\": \"" + offset + "\"";

            if (category != null)
            {
                contentString += ", \"category\": \"" + category + "\"";
            }

            contentString += "}";

            JsonDocument content = JsonDocument.Parse(contentString);

            HttpRequestMessage request = CreateWARequest(endpoint, HttpMethod.Post, WorldAnvilUserApiToken, WorldAnvilAPIKey, content);

            var response = await WorldAnvilApiClient.HttpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // try again with Cloudflair worker
                string cloudflairEndpoint = $"{CloudflairWorkerBaseUrl}{url}";

                if (!string.IsNullOrEmpty(id))
                {
                    cloudflairEndpoint += $"?id={id}";
                }

                HttpRequestMessage cloudflairRequest = CreateWARequest(cloudflairEndpoint, HttpMethod.Post, WorldAnvilUserApiToken, WorldAnvilAPIKey, content);
                response = await WorldAnvilApiClient.HttpClient.SendAsync(cloudflairRequest);
            }

            response.EnsureSuccessStatusCode();

            // Read the response content
            JsonDocument responseData = await response.Content.ReadAsStringAsync()
                is string jsonString
                ? JsonDocument.Parse(jsonString)
                : throw new InvalidOperationException("Failed to read response content.");

            response.Dispose();

            List<JsonDocument> elements = [];
            foreach (JsonElement element in responseData.RootElement.GetProperty("entities").EnumerateArray())
            {
                elements.Add(JsonDocument.Parse(element.GetRawText()));
            }

            return elements;
        }


        public static Dictionary<string, string> FlattenJson(JsonElement element, string path = "")
        {
            var result = new Dictionary<string, string>();

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty prop in element.EnumerateObject())
                    {
                        string newPath = string.IsNullOrEmpty(path)
                            ? $"/{prop.Name}"
                            : $"{path}/{prop.Name}";

                        foreach (var kvp in FlattenJson(prop.Value, newPath))
                            result[kvp.Key] = kvp.Value;
                    }
                    break;

                case JsonValueKind.Array:
                    int i = 0;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        string newPath = $"{path}[{i++}]";
                        foreach (var kvp in FlattenJson(item, newPath))
                            result[kvp.Key] = kvp.Value;
                    }
                    break;

                default:
                    result[path] = element.ToString();
                    break;
            }

            return result;
        }

        public static HttpRequestMessage CreateWARequest(string endpoint, HttpMethod method, string waUserToken, string waApiKey, JsonDocument? content = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(endpoint, nameof(endpoint));
            ArgumentException.ThrowIfNullOrEmpty(waUserToken, nameof(waUserToken));
            ArgumentException.ThrowIfNullOrEmpty(waApiKey, nameof(waApiKey));

            var request = new HttpRequestMessage(method, endpoint);

            request.Headers.Add("x-auth-token", waUserToken);
            request.Headers.Add("x-application-key", waApiKey);

            // Add the User-Agent header
            request.Headers.Add("User-Agent", "RealmStudio");

            if (content != null)
            {
                request.Content = new StringContent(content.RootElement.GetRawText(), Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            return request;
        }
    }

    public static class JsonDocumentExtensions
    {
        public static T? ToObject<T>(this JsonDocument document, JsonSerializerOptions? options = null)
        {
            return document.RootElement.Deserialize<T>(options);
        }

        public static T? ToObject<T>(this JsonElement element, JsonSerializerOptions? options = null)
        {
            return element.Deserialize<T>(options);
        }

        public static readonly JsonSerializerOptions JsonConversionOptions;

        static JsonDocumentExtensions()
        {
            JsonConversionOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            JsonConversionOptions.Converters.Add(new FlexibleNullableIntConverter());
        }
    }


    public class FlexibleNullableIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int number))
                        return number;
                    break;

                case JsonTokenType.String:
                    var str = reader.GetString();
                    if (int.TryParse(str, out int parsed))
                        return parsed;
                    break;

                case JsonTokenType.Null:
                    return null;
            }

            // Fallback if value is unrecognized (e.g. "N/A")
            return null;
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }


    #endregion

    #region WORLD ANVIL DATA MODELS
    //==============================================================================================//
    // World Anvil Data Models

    public abstract class WorldAnvilGetResult
    {
        public int? granularity { get; set; }
    }
    public class WorldAnvilArticle : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
        public string? position { get; set; }
        public string? excerpt { get; set; }
        public int? wordcount { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public WorldAnvilDate? publicationDate { get; set; }
        public WorldAnvilDate? notificationDate { get; set; }
        public List<WorldAnvilUser>? likes { get; set; }
        public int? views { get; set; }
        public string? userMetadata { get; set; }
        public string? articleMetadata { get; set; }
        public string? cssClasses { get; set; }
        public string? displayCss { get; set; }
        public string? templateType { get; set; }
        public string? customArticleTemplate { get; set; }
        public string? content { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilCategory? category { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public string? pronunciation { get; set; }
        public string? snippet { get; set; }
        public string? seeded { get; set; }
        public string? sidebarcontent { get; set; }
        public string? sidepanelcontenttop { get; set; }
        public string? sidepanelcontent { get; set; }
        public string? sidebarcontentbottom { get; set; }
        public string? footnotes { get; set; }
        public string? fullfooter { get; set; }
        public string? authornotes { get; set; }
        public string? scrapbook { get; set; }
        public string? credits { get; set; }
        public bool? displaySidebar { get; set; }
        public WorldAnvilTimeline? timeline { get; set; }
        public WorldAnvilPrompt? prompt { get; set; }
        public bool? showSeeded { get; set; }
        public bool? webhookUpdate { get; set; }
        public bool? communityUpdate { get; set; }
        public string? commentPlaceholder { get; set; }
        public string? metaTitle { get; set; }
        public string? metadDescription { get; set; }
        public string? subheading { get; set; }
        public bool? coverIsMap { get; set; }
        public bool? isFeaturedArticle { get; set; }
        public bool? isAdultContent { get; set; }
        public bool? isLocked { get; set; }
        public bool? allowComments { get; set; }
        public bool? showInToc { get; set; }
        public bool? isEmphasized { get; set; }
        public bool? displayAuthor { get; set; }
        public bool? displayChildrenUnder { get; set; }
        public bool? displayTitle { get; set; }
        public bool? displaySheet { get; set; }
        public string? badge { get; set; }
        public List<WorldAnvilSecret>? secrets { get; set; }
        public List<WorldAnvilHistory>? histories { get; set; }
        public string? editURL { get; set; }
        public WorldAnvilImage? cover { get; set; }
        public WorldAnvilGallery? gallery { get; set; }
        public string? articleNext { get; set; }
        public string? articlePrevious { get; set; }
        public WorldAnvilBlock? block { get; set; }
        public WorldAnvilOrgChart? orgchart { get; set; }
        public List<WorldAnvilManuscript>? manuscripts { get; set; }
        public WorldAnvilAncestry? ancestry { get; set; }
    }

    public class WorldAnvilBlock : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public string? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public string? updateDate { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
        public string? identifier { get; set; }
        public string? dataParser { get; set; }
        public string? textualdata { get; set; }
        public string? tabulardata { get; set; }
        public string? jsondata { get; set; }
        public string? isShared { get; set; }
        public string? isSRD { get; set; }
        public WorldAnvilBlockTemplate? template { get; set; }
        public WorldAnvilRPGSystem? RPGSRD { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilBlockFolder? folder { get; set; }
    }

    public class WorldAnvilBlockTemplate : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? isEditable { get; set; }         // WA Swagger documentation says this is a string; it is probably actually boolean
        public string? success { get; set; }            // WA Swagger documentation says this is a string; it is probably actually boolean
        public string? listtitle { get; set; }
        public string? identifier { get; set; }
        public string? description { get; set; }
        public string? articleTemplate { get; set; }
        public string? formSchemaParser { get; set; }
        public string? formSchema { get; set; }
        public string? formDisplayStructure { get; set; }
        public string? displayStructure { get; set; }
        public string? displayBadge { get; set; }
        public string? displayStyling { get; set; }
        public string? displayScripting { get; set; }
        public string? displayTrackable { get; set; }
        public string? displayStylingRaw { get; set; }
        public bool? hasRawJson { get; set; }

        // TODO: documentation at https://www.worldanvil.com/api/external/boromir/swagger-documentation#/Block%20Template/readBlockTemplate
        // is missing schema for granularity 1 and 2
    }

    public class WorldAnvilBlockTemplatePart : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
        public string? type { get; set; }
        public string? description { get; set; }
        public string? placeholder { get; set; }
        public int[]? required { get; set; }
        public string? cssClass { get; set; }
        public int? position { get; set; }
        public string? section { get; set; }
        public int? rows { get; set; }
        public string? renderer { get; set; }
        public int? min { get; set; }
        public int? max { get; set; }
        public string? options { get; set; }
        public string? trackable { get; set; }
        public WorldAnvilBlockTemplate? template { get; set; }
    }

    public class WorldAnvilBlockFolder : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
        public string? identifier { get; set; }
        public List<WorldAnvilBlock>? blocks { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilWorld? world { get; set; }
    }

    public class WorldAnvilCategory : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? state { get; set; }
        public string? icon { get; set; }
        public List<JsonDocument>? subscribergroups { get; set; }
        public string? description { get; set; }
        public string? excerpt { get; set; }
        public bool? isBook { get; set; }
        public bool? displayBookTitle { get; set; }
        public bool? isCollapsed { get; set; }
        public int? position { get; set; }
        public string? custom1 { get; set; }
        public string? custom2 { get; set; }
        public string? custom3 { get; set; }
        public string? custom4 { get; set; }
        public string? custom5 { get; set; }
        public string? cssClasses { get; set; }
        public string? systemMeta { get; set; }
        public WorldAnvilImage? pagecover { get; set; }
        public WorldAnvilImage? bookcover { get; set; }
        public WorldAnvilImage? defaultarticlecover { get; set; }
        public WorldAnvilCategory? parent { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilArticle? articleRedirect { get; set; }
        public string? editurl { get; set; }
    }

    public class WorldAnvilCanvas : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public string? updateDate { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public WorldAnvilCanvasPageData? data { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
    }

    public class WorldAnvilCanvasData
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? name { get; set; }
        public string? uuid { get; set; }
        public List<WorldAnvilCanvasPageData>? pages { get; set; }
        public string? state { get; set; }
        public string? title { get; set; }
        public List<string>? assets { get; set; }
        public int? version { get; set; }
        public WorldAnvilCanvasSettingsData? settings { get; set; }
        public string? pageStates { get; set; }
        public WorldAnvilCanvasUserSettings? userSettings { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
    }

    public class WorldAnvilCanvasPageData
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? name { get; set; }
        public string? shapes { get; set; }
    }

    public class WorldAnvilCanvasSettingsData
    {
        public bool? showGrid { get; set; }
        public bool? isPenMode { get; set; }
        public bool? isDarkMode { get; set; }
        public bool? isSnapping { get; set; }
        public bool? isZoomSnap { get; set; }
        public bool? isDebugMode { get; set; }
        public bool? isFocusMode { get; set; }
        public bool? isReadonlyMode { get; set; }
        public bool? showCloneHandles { get; set; }
        public bool? showRotateHandles { get; set; }
        public int? nudgeDistanceLarge { get; set; }
        public int? nudgeDistanceSmall { get; set; }
        public bool? showBindingHandles { get; set; }
    }

    public class WorldAnvilCanvasUserSettings
    {
        public string? tlBgColor { get; set; }
        public bool? tlShowText { get; set; }
        public string[]? recentIcons { get; set; }
        public string? DANGEROUSLY_SET_CSS { get; set; }
    }

    public class WorldAnvilTimeline : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public string? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? histories { get; set; }
        public string? description { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public string? utdOffset { get; set; }
        public string? type { get; set; }
        public string? showInToc { get; set; }
        public string? views { get; set; }
        public string? likes { get; set; }
        public string? presentationParameters { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilArticle? article { get; set; }
        public WorldAnvilCategory? category { get; set; }
        public WorldAnvilCalendar? calendar { get; set; }
        public string? eras { get; set; }
        public WorldAnvilUser? author { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
    }

    public class WorldAnvilCalendar
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilHistory : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }

        // TODO: there may be more fields; the Swagger documentation page for History is throwing an error when rendering the schema
    }

    public class WorldAnvilUser : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? enabled { get; set; }
        public string? token { get; set; }
        public string? username { get; set; }
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public WorldAnvilDate? lastLogin { get; set; }
        public string? bio { get; set; }
        public string? locale { get; set; }
        public string? primaryUseType { get; set; }
        public int? onboardingProgress { get; set; }
        public string? signature { get; set; }
        public string? customProfileContent { get; set; }
        public WorldAnvilDate? registerDate { get; set; }
        public WorldAnvilDate? membershipDate { get; set; }
        public bool? membership { get; set; }
        public string? membershipType { get; set; }
        public string? websiteurl { get; set; }
        public string? favMovies { get; set; }
        public string? favSeries { get; set; }
        public string? favBooks { get; set; }
        public string? favWriters { get; set; }
        public string? favGames { get; set; }
        public string? interests { get; set; }
        public string? nanowrimo { get; set; }
        public string? twitter { get; set; }
        public string? facebook { get; set; }
        public string? reddit { get; set; }
        public string? tumblr { get; set; }
        public string? pinterest { get; set; }
        public string? deviantart { get; set; }
        public string? youtube { get; set; }
        public string? vimeo { get; set; }
        public string? google { get; set; }
        public string? steam { get; set; }
        public string? twitch { get; set; }
        public string? discord { get; set; }
        public string? instagram { get; set; }
        public string? kofi { get; set; }
        public string? patreon { get; set; }
        public string? mastodon { get; set; }
        public string? bluesky { get; set; }
        public int? views { get; set; }
        public string? openText { get; set; }
        public string? profileMeta { get; set; }
        public bool? isVenerable { get; set; }
        public bool? isPremier { get; set; }
        public bool? isLifetime { get; set; }
        public int? anvilCoinsCurrent { get; set; }
        public string? email { get; set; }
        public string? timezone { get; set; }
        public string? location { get; set; }
        public string? dob { get; set; }
        public int? worldsLimit { get; set; }
        public int? campaignsLimit { get; set; }
        public int? uploadSizeLimit { get; set; }
        public string? storageSpaceLimit { get; set; }          // this should be an int, but the JSON is returning it as a string
        public int? coauthors { get; set; }
        public int? subscriberSlots { get; set; }
        public bool? allowAdultContent { get; set; }
        public int? featureWorldbuilding { get; set; }
        public int? featureRPG { get; set; }
        public int? featureCommunity { get; set; }
        public int? featureHeroes { get; set; }
        public int? featureWriting { get; set; }
        public int? featurePrompts { get; set; }
        public int? featureAutosave { get; set; }
        public int? featureExpandedArticle { get; set; }
        public bool? isCompetitor { get; set; }
        public int? interfaceVersion { get; set; }
        public string? interfaceVignetteRows { get; set; }
        public string? interfaceFormBackground { get; set; }
        public string? interfaceFormColor { get; set; }
        public string? interfaceFormFontSize { get; set; }
        public bool? interfaceActivateAdvancedSelect { get; set; }
        public bool? interfaceActivateAccessibility { get; set; }
        public WorldAnvilManuscriptSettings? manuscriptSettings { get; set; }
        public string? interfaceTheme { get; set; }
        public string? interfaceEditor { get; set; }
        public string? interfaceEditorMode { get; set; }
        public string? interfaceEditorTheme { get; set; }
        public bool? interfaceShowSaveIndicator { get; set; }
        public string? discordToken { get; set; }
        public string? discordRefreshToken { get; set; }
        public string? discordTokenExpiry { get; set; }
        public string? discordUserId { get; set; }
        public bool? isNewsletter { get; set; }
        public WorldAnvilMembershipPrototype? membershipPrototype { get; set; }
        public string? chapterhouse { get; set; }
        public WorldAnvilWorld? activeWorld { get; set; }
        public string? activeCampaign { get; set; }
        public string? activeCharacter { get; set; }
        public string? activeSession { get; set; }
        public WorldAnvilImage? avatar { get; set; }
        public WorldAnvilImage? cover { get; set; }
        public List<string>? roles { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilWorld : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? descriptionParsed { get; set; }
        public WorldAnvilUser? owner { get; set; }
        public int? countFollowers { get; set; }
        public int? countArticles { get; set; }
        public int? countMaps { get; set; }
        public int? countTimelines { get; set; }
        public string? subtitle { get; set; }
        public string? locale { get; set; }
        public string? description { get; set; }
        public string? excerpt { get; set; }
        public bool? isStored { get; set; }
        public string? displayCss { get; set; }
        public string? displayPanelCss { get; set; }
        public string? copyright { get; set; }
        public string? worldSidebarContent { get; set; }
        public string? globalAnnouncement { get; set; }
        public string? globalHeader { get; set; }
        public string? globalSidebarFooter { get; set; }
        public string? globalArticleIntroduction { get; set; }
        public WorldAnvilImage? cover { get; set; }
        public List<WorldAnvilGenre>? genres { get; set; }
        public WorldAnvilTheme? theme { get; set; }
        public string? activationDescription { get; set; }
        public int? weight { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public string? timeNeg { get; set; }
        public string? timePos { get; set; }
        public string? timeNegAbbr { get; set; }
        public string? timePosAbbr { get; set; }
        public string? timeCurrentYear { get; set; }
        public int? timeCurrentMonth { get; set; }
        public int? timeCurrentDay { get; set; }
        public string? timeCurrentDescription { get; set; }
        public string? worldDateFormat { get; set; }
        public bool? displayRecentArticles { get; set; }
        public bool? displayOtherArticles { get; set; }
        public bool? displayArticleTooltips { get; set; }
        public bool? displaySharingButtons { get; set; }
        public bool? displayCommunityFeatures { get; set; }
        public bool? displayCampaigns { get; set; }
        public bool? displayHeroes { get; set; }
        public bool? displayBlockHeaders { get; set; }
        public bool? displaySecureLinks { get; set; }
        public string? placementToc { get; set; }
        public string? placementMaps { get; set; }
        public string? placementTimelines { get; set; }
        public string? homepageContent1 { get; set; }
        public string? homepageContent2 { get; set; }
        public string? homepageContent3 { get; set; }
        public string? homepageContent4 { get; set; }
        public string? displayJs { get; set; }
        public DisplayStyles? displayStyles { get; set; }
        public bool? displayDefaultTheme { get; set; }
        public string? displayBootstrapVersion { get; set; }
        public string? twitter { get; set; }
        public string? facebook { get; set; }
        public string? patreon { get; set; }
        public string? twitch { get; set; }
        public string? discord { get; set; }
        public int? views { get; set; }
        public bool? isWhitelabel { get; set; }
        public bool? isCommunity { get; set; }
        public bool? isFeatured { get; set; }
        public string? gatrackingcode { get; set; }
        public string? followersNominal { get; set; }
        public string? followersPlural { get; set; }
        public bool? displayWorldMeta { get; set; }
        public bool? openSecrets { get; set; }
        public bool? articlesPrivateOnCreation { get; set; }
        public string? worldCreditsOverride { get; set; }
        public WorldAnvilArticle? accessDeniedPage { get; set; }
        public WorldAnvilImage? globalcover { get; set; }
        public WorldAnvilMap? worldmap { get; set; }
        public List<WorldAnvilCustomArticleTemplate>? customArticleTemplates { get; set; }
        public string? defaultCalendar { get; set; }
        public string? metaTitle { get; set; }
        public string? metaDescription { get; set; }
        public string? worldScrapBook { get; set; }
        public string? foundationMotivation { get; set; }
        public string? foundationGoal { get; set; }
        public string? foundationUsp { get; set; }
        public string? foundationThemeGenre { get; set; }
        public string? foundationThemeFeel { get; set; }
        public string? foundationThemeTone { get; set; }
        public string? foundationThemeThemes { get; set; }
        public string? foundationThemeAgency { get; set; }
        public string? foundationMetaExpanded { get; set; }
        public string? foundationDramaExpanded { get; set; }
        public string? settingRules { get; set; }
        public string? settingCosmology { get; set; }
        public string? settingGeography { get; set; }
        public string? settingSize { get; set; }
        public string? settingPeopleAndHistory { get; set; }
        public string? settingPeopleSpeciesAndCultures { get; set; }
        public string? settingNeedsAndRelations { get; set; }
        public string? inspirationImages { get; set; }
        public string? inspirationMusic { get; set; }
        public string? inspirationBooks { get; set; }
        public string? inspirationMoviesAndTv { get; set; }
        public bool? markCompleteWorldMeta { get; set; }
        public bool? lockSlug { get; set; }
        public string? prepareJs { get; set; }
        public int? goalWords { get; set; }
        public string? webhookGlobal { get; set; }
        public string? accessCode { get; set; }
        public bool? activateVisibilityToggle { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilMap : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? description { get; set; }
        public string? legend { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public bool? isDeleted { get; set; }
        public bool? displayOnHomepage { get; set; }
        public int? views { get; set; }
        public int? position { get; set; }
        public int? zoomOriginal { get; set; }
        public int? zoomMin { get; set; }
        public int? zoomMax { get; set; }
        public int? centerY { get; set; }
        public int? centerX { get; set; }
        public WorldAnvilCategory? category { get; set; }
        public WorldAnvilImage? image { get; set; }
        public WorldAnvilImage? compass { get; set; }
        public WorldAnvilImage? thumbnail { get; set; }
        public WorldAnvilArticle? organization { get; set; }
        public WorldAnvilArticle? location { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public List<WorldAnvilMapMarker>? markers { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilMapLayer : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public string? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public string? updateDate { get; set; }
        public string? description { get; set; }
        public string? position { get; set; }
        public WorldAnvilImage? image { get; set; }
        public WorldAnvilMap? map { get; set; }
        public WorldAnvilMarkerGroup? group { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilUser? author { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
    }

    public class WorldAnvilMapMarker : WorldAnvilGetResult
    {
        // TODO: there are probably other fields; the Swagger documentation doesn't provide the schema for Markers
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public bool? state { get; set; }
        public bool? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilMarkerGroup : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? activeOnLoad { get; set; }
        public int? position { get; set; }
        public WorldAnvilMap? map { get; set; }
        public WorldAnvilUser? author { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilMarkerType
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public int? iconSizeWidth { get; set; }
        public int? iconSizeHeight { get; set; }
        public int? shadowSizeWidth { get; set; }
        public int? shadowSizeHeight { get; set; }
        public int? iconAnchorX { get; set; }
        public int? iconAnchorY { get; set; }
        public int? popupAnchorX { get; set; }
        public int? popupAnchorY { get; set; }
        public string? className { get; set; }
        public string? identifier { get; set; }
        public bool? isGuild { get; set; }
        public bool? isPublic { get; set; }
        public string? theme { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public WorldAnvilImage? pin { get; set; }
        public WorldAnvilImage? shadow { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilUser? author { get; set; }
        public string? isEditable { get; set; }
        public string? success { get; set; }
    }

    public class WorldAnvilManuscript : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptBeat : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Beat is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptBookmark : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Bookmark is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptPart : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Part is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptVersion : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Version is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptTag : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Tag is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptStat : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Stat is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptLabel : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Label is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilManuscriptPlot : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        // TODO: Swagger documentation for Manuscript_Plot is not rendering; seems to be an error in the website code
    }

    public class WorldAnvilNotebook : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public string? color { get; set; }
        public int? weight { get; set; }
        public int? isSelected { get; set; }                            // Swagger documentation says int, but this is probably a bool
        public List<WorldAnvilNoteSection>? notesections { get; set; }
        public WorldAnvilImage? cover { get; set; }
        public WorldAnvilCampaignReference? campaign { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilUser? author { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilNoteSection : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public string? color { get; set; }
        public int? weight { get; set; }
        public int? isSelected { get; set; }                            // Swagger documentation says int, but this is probably a bool
        public List<WorldAnvilNote>? notes { get; set; }
        public WorldAnvilNotebook? notebook { get; set; }
        public WorldAnvilUser? author { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilNote : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? content { get; set; }
        public string? importance { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public string? color { get; set; }
        public string? weight { get; set; }
        public string? isSelected { get; set; }
        public string? image { get; set; }
        public string? campaign { get; set; }
        public string? session { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilArticle? article { get; set; }
        public WorldAnvilNoteSection? notesection { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilPrompt
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
    }

    public class WorldAnvilSecret : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? content { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public WorldAnvilUser? author { get; set; }
        public WorldAnvilArticle? article { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public string? editURL { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilOrgChart : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
    }


    public class WorldAnvilImage : WorldAnvilGetResult
    {
        public int id { get; set; }     // id fields for images are integers, unlike every other World Anvil object
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
        public string? filename { get; set; }
        public string? path { get; set; }
        public int? size { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string? extension { get; set; }
        public string? description { get; set; }
        public string? alt { get; set; }
        public string? creditArtistName { get; set; }
        public string? creditArtistWebsite { get; set; }
        public string? creditArtTitle { get; set; }
        public string? creditArtUrl { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public int? views { get; set; }
        public int? likes { get; set; }
        public bool? isFeatured { get; set; }
        public string? linkUrl { get; set; }
        public string? pageUrl { get; set; }
        public WorldAnvilArticle? article { get; set; }
        public WorldAnvilUser? owner { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public WorldAnvilCharacter? character { get; set; }
        public List<WorldAnvilGallery>? galleries { get; set; }
    }

    public class WorldAnvilGallery : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilCharacter
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilTheme : WorldAnvilGetResult
    {
        public int? id;
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? isWip { get; set; }
        public string? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilGenre
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
    }

    public class WorldAnvilSubscriberGroup : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? description { get; set; }
        public int? position { get; set; }
        public bool? isDefault { get; set; }
        public WorldAnvilDate? creationDate { get; set; }
        public bool? isHidden { get; set; }
        public bool? isAssignable { get; set; }
        public string? campaign { get; set; }
        public string? party { get; set; }
        public WorldAnvilWorld? world { get; set; }
        public List<WorldAnvilUser>? paidsubscribers { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilRPGSystem : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public string? reference { get; set; }
        public string? description { get; set; }
        public string? publisher { get; set; }
        public string? copyright { get; set; }
        public string? logo { get; set; }
        public string? styles { get; set; }
        public bool? heroesEnabled { get; set; }
        public string? weight { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
    }

    public class WorldAnvilCampaignReference
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilVariable : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        // Swagger documentation is not being rendered
    }

    public class WorldAnvilVariableCollection : WorldAnvilGetResult
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }

        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
        public bool? isEditable { get; set; }
        public bool? success { get; set; }
        public string? description { get; set; }
        public string? prefix { get; set; }
        public WorldAnvilWorld? world { get; set; }
    }


    public class WorldAnvilCustomArticleTemplate
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
    }

    public class WorldAnvilMembershipPrototype
    {
        private Guid? _id;

        public string? id
        {
            get => _id?.ToString();
            set => _id = value is not null ? Guid.Parse(value) : null;
        }
        public string? title { get; set; }
        public string? slug { get; set; }
        public string? state { get; set; }
        public bool? isWip { get; set; }
        public bool? isDraft { get; set; }
        public string? entityClass { get; set; }
        public string? icon { get; set; }
        public string? url { get; set; }
        public List<WorldAnvilSubscriberGroup>? subscribergroups { get; set; }
        public string? folderId { get; set; }
        public string? tags { get; set; }
        public WorldAnvilDate? updateDate { get; set; }
    }

    public class WorldAnvilManuscriptSettings
    {
        public string? fontSize { get; set; }
        public string? lineHeight { get; set; }
        public string? paragraphyPadding { get; set; }
        public string? cssRules { get; set; }
        public string? fontTypeface { get; set; }
        public string? paragraphIndent { get; set; }
        public string? backgroundColor { get; set; }
        public string? fontColor { get; set; }
        public string? paragraphPadding { get; set; }
    }

    public class WorldAnvilDate
    {
        public string? date { get; set; }
        public string? timezone { get; set; }
        public int? timezone_type { get; set; }
    }

    public class WorldAnvilAncestry
    {
        public JsonDocument? firstUp { get; set; }
        public JsonDocument? secondUp { get; set; }
        public JsonDocument? thirdUp { get; set; }
    }

    public class DisplayStyles
    {
        public string? display_background { get; set; }
        public string? display_background_id { get; set; }
        public string? display_base_font_color { get; set; }
        public string? display_base_font_size { get; set; }
    }

    #endregion WORLD ANVIL DATA MODELS
}
