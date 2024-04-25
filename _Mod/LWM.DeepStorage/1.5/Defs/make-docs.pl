#!/usr/bin/perl
# A little script to make wiki documentation easier for GitHub
# Use:
# ./make-docs.pl def_files.xml more_def_files.xml
# copy and paste output
# Pretty much designed for Deep Storage.

use strict;
use XML::Parser;

my $parser = new XML::Parser;

$parser->setHandlers( Start=>\&startTag,
    End=>\&endTag,
    Char=>\&charData,);

my $printing=0;
my $inStorageSettings=0;

while (my $xmlfile=shift) {
    die "Cannot find file \"$xmlfile\""
        unless -f $xmlfile;
    $parser->parsefile($xmlfile);
}




sub startTag {
    my ($parser, $tag, %attributes)=@_;
#    print "Tag is $tag\n";
    if ($tag eq "label") {
        print "\n## ";
        $printing="true";        
    }
    $inStorageSettings=1 if ($tag eq "fixedStorageSettings");
    if ($inStorageSettings and ($tag eq "li")) {
        $printing=1;
        print "* ";
    }
    if ($inStorageSettings and ($tag eq "disallowedCategories" or $tag eq "disallowedSpecialFilters")) {
        print "\nBut not:\n";
    }
}

sub endTag {
    my ($parser, $tag)=@_;
    if ($printing) {print "\n";}    
    $printing=0;
    if ($tag eq "label") { print "Can store:\n"; }
    if ($inStorageSettings and ($tag eq "fixedStorageSettings")) {
        $inStorageSettings=0;
    }
}

sub charData {
    my( $parseinst, $data ) = @_;
    if ($printing) {print "$data";}

}
