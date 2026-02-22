import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useFilesystemBrowse(path?: string) {
  return useQuery({
    queryKey: ["filesystem", "browse", path ?? ""],
    queryFn: () => api.browseFilesystem(path),
  });
}
