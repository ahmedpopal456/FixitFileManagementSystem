using AutoMapper;
using System;
using System.IO;
using System.Linq;
using System.Web;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Responses;
using Fixit.Core.Storage.DataContracts.FileSystem.Models;
using Fixit.Core.Storage.DataContracts.Helpers;
using Fixit.Core.Storage.DataContracts.TableEntities;

namespace Fixit.FileManagement.Lib.Mappers
{
  public class FileManagementMapper : Profile
  {
    public FileManagementMapper()
    {
      CreateMap<FileMetadata, FileSystemFileDto>()
        .ForMember(dto => dto.Id, opt => opt.MapFrom(fileMetadata => fileMetadata.FileId.ToString()))
        .ForMember(dto => dto.FileTags, opt => opt.MapFrom(fileMetadata => fileMetadata.Tags));       

      CreateMap<FileSystemFileDto, FileMetadataSummary>()
        .ForMember(fileMetadataSummary => fileMetadataSummary.TagNames, opt=>opt.MapFrom(dto=> string.Join(",", dto.FileTags.Select(t=> t.Name))));

      CreateMap<FileUploadDto, FileUploadResponseDto>();
      CreateMap<FileUploadDto, TableFileEntity>()
        .ForMember(fileEntity => fileEntity.FileName, opts => opts.MapFrom(dto => dto.FileCreatedName))
        .ForMember(fileEntity => fileEntity.FolderPath, opts => opts.MapFrom(dto => StringHelper.ToAzureDirectoryPath(Path.GetDirectoryName(dto.FileCreatedPath))))
        .ForMember(fileEntity => fileEntity.FileSize, opts => opts.MapFrom(dto => dto.FileCreatedLength.ToString()))
        .ForMember(fileEntity => fileEntity.RowKey, opts => opts.MapFrom(dto => dto.FileCreatedId.ToString()))
        .ForMember(fileEntity => fileEntity.CreatedAtTimestampUtc, opts => opts.MapFrom(dto => dto.FileCreatedTimestampUtc));

      CreateMap<FileMetadataSummary, FileMetadataDto>()
        .ForMember(fileMetadata => fileMetadata.MetadataExtension, opts => opts.MapFrom(dto => dto.MetadataExtension))
        .ForMember(fileMetadata => fileMetadata.Tags, opts => opts.MapFrom(dto => string.IsNullOrWhiteSpace(dto.TagNames) ? null : dto.TagNames.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Distinct().Select(tag => new FileTagDto() { Name = tag.Trim().ToLowerInvariant()})))
        .ReverseMap();

      CreateMap<FileMetadata, FileInfoDto>()
        .ForMember(fileEntity => fileEntity.ThumbnailUrl, opts => opts.MapFrom(dto => HttpUtility.UrlDecode(dto.ThumbnailUrl)));

      CreateMap<FileSystemDirectoryDto, FileSystemRootDirectoryDto>()
        .ForMember(fileEntity => fileEntity.DirectoryInfo, opts => opts.MapFrom(dto => dto))
        .ForMember(fileEntity => fileEntity.DirectoryTags, opts => opts.Ignore());

      CreateMap<FileSystemDirectoryDto, FileSystemDirectoryItemsDto>()
        .ForMember(fileEntity => fileEntity.DirectoryItems, opts => opts.MapFrom(dto => dto.DirectoryItems));

      CreateMap<FileMetadata, FileToRegenerateUrlDto>()
        .ForMember(dto => dto.EntityId, opt => opt.MapFrom(src => long.Parse(src.EntityId)));

      CreateMap<FileSystemFileDto, FileToRegenerateUrlDto>()
       .ForMember(dto => dto.EntityId, opt => opt.MapFrom(src => long.Parse(src.EntityId)))
       .ForMember(dto => dto.FileId, opt => opt.MapFrom(src => Guid.Parse(src.Id)));

    }
  }
}