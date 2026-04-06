import { getAllDocs } from "@/lib/docs";
import { Header } from "./header";
import { SearchDialog } from "@/components/docs/search-dialog";

export function HeaderWithSearch() {
  const docs = getAllDocs().map((d) => ({
    slug: d.slug,
    title: d.title,
    description: d.description,
    content: d.content,
  }));

  return <Header searchSlot={<SearchDialog docs={docs} />} />;
}
